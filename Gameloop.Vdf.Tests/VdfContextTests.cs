using Xunit;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace Gameloop.Vdf.Tests
{
    public class VContextTests
    {
        #region Concrete Implementations for Testing

        private class TestBinaryReader(Stream s) : VdfBinaryReader(s);
        private class TestBinaryWriter(Stream s) : VdfBinaryWriter(s);

        private class TestTextReader(TextReader r, VdfSerializerSettings s) : VdfReader(s)
        {
            private readonly TextReader _reader = r;
            public override bool ReadToken() => false;
            public override Task<bool> ReadTokenAsync() => Task.FromResult(false);
            public override void Close()
            {
                if (CloseInput) _reader.Dispose();
                base.Close();
            }
        }

        #endregion

        #region State & Lifecycle Tests

        [Fact]
        public void VdfState_EnumConstants_AreCorrect()
        {
            Assert.Equal(0, (int)VdfState.Start);
            Assert.Equal(11, (int)VdfState.Finished);
            Assert.Equal(12, (int)VdfState.Closed);
            Assert.Equal(VdfState.Start, VdfState.Start);
        }

        [Fact]
        public void VdfReader_Dispose_ClosesInputByDefault()
        {
            var ms = new MemoryStream();
            using (var reader = new TestTextReader(new StreamReader(ms), VdfSerializerSettings.Default))
            {
                Assert.Equal(VdfState.Start, reader.CurrentState);
            }
            Assert.Throws<ObjectDisposedException>(() => ms.Position);
        }

        [Fact]
        public async Task BinaryIO_Dispose_ClosesUnderlyingStream()
        {
            var ms = new MemoryStream();
            var writer = new TestBinaryWriter(ms);
            await writer.DisposeAsync();
            Assert.Throws<ObjectDisposedException>(() => ms.Position);
        }

        #endregion

        #region Binary Writer Tests

        [Fact]
        public void BinaryWriter_WritesCorrectTypeMarkersAndStructure()
        {
            using var ms = new MemoryStream();
            var writer = new TestBinaryWriter(ms);
            var prop = new VProperty("Player", new VObject
            {
                new VProperty("Health", new VValue(100))
            });

            writer.Write(prop);

            byte[] result = ms.ToArray();
            Assert.Equal(0x00, result[0]); // Object Start
            Assert.Contains((byte)0x02, result); // Int32 marker
            Assert.Equal(0x0B, result[^1]); // EOF marker
        }

        [Theory]
        [InlineData(123, 0x02)]
        [InlineData(3.14f, 0x03)]
        [InlineData(123456789UL, 0x07)]
        public void BinaryWriter_CorrectlySerializesNumericTypes(object value, byte expectedType)
        {
            using var ms = new MemoryStream();
            var writer = new TestBinaryWriter(ms);
            var prop = new VProperty("Key", new VValue(value));

            writer.Write(prop);

            byte[] result = ms.ToArray();
            Assert.Equal(expectedType, result[0]);
        }

        [Fact]
        public async Task BinaryWriterAsync_WritesNullTerminatedStrings()
        {
            using var ms = new MemoryStream();
            var writer = new TestBinaryWriter(ms);
            var prop = new VProperty("Name", new VValue("Gordon"));

            await writer.WriteAsync(prop);

            string decoded = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("Name\0", decoded);
            Assert.Contains("Gordon\0", decoded);
        }

        #endregion

        #region Binary Reader Tests

        [Fact]
        public void BinaryReader_ReadsObjectHierarchyAndStringsCorrectly()
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x00); ms.Write("Player\0"u8);
            ms.WriteByte(0x02); ms.Write("Health\0"u8); ms.Write(BitConverter.GetBytes(100));
            ms.WriteByte(0x08); ms.WriteByte(0x0B);
            ms.Position = 0;

            var reader = new TestBinaryReader(ms);
            var result = reader.Read();

            Assert.Equal("Player", result.Key);
            var obj = Assert.IsType<VObject>(result.Value);
            Assert.Equal(100, obj.Value<int>("Health"));
        }

        [Fact]
        public async Task BinaryReaderAsync_ResilientToStringStoredNumbers()
        {
            using var ms = new MemoryStream();
            ms.WriteByte(0x01); ms.Write("Key\0"u8); ms.Write("500\0"u8);
            ms.Position = 0;

            var reader = new TestBinaryReader(ms);
            var result = await reader.ReadAsync();

            Assert.Equal(500, result.Value.Convert<VToken, int>());
        }

        #endregion
    }
}
