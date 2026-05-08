using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Gameloop.Vdf.Linq;
using SteamKit2;

namespace Gameloop.Vdf.Benchmarks;

    [MemoryDiagnoser]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class VdfFileBenchmarks
    {
        private string _filePath = null!;
        private string _vdfContent = null!;
        private VProperty _vdfObject = null!;
        private readonly VdfSerializer _serializer = new(VdfSerializerSettings.Common);

        [Params(100, 1000)]
        public int DataSize
        {
            get;
            set => field = value > 0 ? value : 10;
        } = 100;

        [GlobalSetup]
        public void Setup()
        {
            _filePath = Path.GetTempFileName();
            var rootObj = new VObject();
            for (int i = 0; i < DataSize; i++)
                rootObj.Add(new VProperty($"Key_{i}", new VValue($"Value_{i}")));

            _vdfObject = new VProperty("Root", rootObj);

            using var sw = new StringWriter();
            _serializer.Serialize(sw, _vdfObject);
            _vdfContent = sw.ToString();
            File.WriteAllText(_filePath, _vdfContent);
        }

        [GlobalCleanup]
        public void Cleanup() => File.Delete(_filePath);

        [Benchmark]
        public void DeserializeSk2() => KeyValue.LoadFromString(_vdfContent);

        [Benchmark]
        public VProperty DeserializeVdfNet()
        {
            using var stream = File.OpenRead(_filePath);
            using var reader = new StreamReader(stream);
            return _serializer.Deserialize(reader);
        }
    }
