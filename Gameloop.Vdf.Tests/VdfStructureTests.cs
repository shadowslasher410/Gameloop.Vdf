namespace Gameloop.Vdf.Tests
{
    public class VdfStructureTests
    {
        [Theory]
        [InlineData('\n', true)]
        [InlineData('\t', true)]
        [InlineData('\v', true)]
        [InlineData('\b', true)]
        [InlineData('\r', true)]
        [InlineData('\f', true)]
        [InlineData('\a', true)]
        [InlineData('\\', true)]
        [InlineData('?', true)]
        [InlineData('\'', true)]
        [InlineData('\"', true)]
        [InlineData('a', false)]
        [InlineData('1', false)]
        [InlineData(' ', false)]
        [InlineData('z', false)]
        public void IsVdfEscapable_IdentifiesValidCharacters(char input, bool expected)
        {
            Assert.Equal(expected, input.IsVdfEscapable());
        }

        [Theory]
        [InlineData('\n', 'n')]
        [InlineData('\t', 't')]
        [InlineData('\r', 'r')]
        [InlineData('\a', 'a')]
        [InlineData('\\', '\\')]
        [InlineData('\"', '\"')]
        [InlineData('z', 'z')]
        public void ToVdfEscape_ReturnsCorrectMappedCharacter(char input, char expected)
        {
            Assert.Equal(expected, input.ToVdfEscape());
        }

        [Theory]
        [InlineData('n', '\n')]
        [InlineData('t', '\t')]
        [InlineData('r', '\r')]
        [InlineData('a', '\a')]
        [InlineData('\"', '\"')]
        [InlineData('?', '?')]
        [InlineData('x', 'x')]
        public void FromVdfEscape_ReturnsCorrectMappedCharacter(char input, char expected)
        {
            Assert.Equal(expected, input.FromVdfEscape());
        }

        [Fact]
        public void VdfEscape_RoundTrip_WorksForCommonCharacters()
        {
            char original = '\n';
            char escaped = original.ToVdfEscape();
            Assert.Equal('n', escaped);
            Assert.Equal(original, escaped.FromVdfEscape());
        }

        [Fact]
        public void VdfStructure_Constants_MatchStandard()
        {
            Assert.Equal('{', VdfStructure.ObjectStart);
            Assert.Equal('}', VdfStructure.ObjectEnd);
            Assert.Equal('"', VdfStructure.Quote);
            Assert.Equal('/', VdfStructure.Comment);
            Assert.Equal('$', VdfStructure.ConditionalConstant);
            Assert.Equal('!', VdfStructure.ConditionalNot);
        }

        [Theory]
        [InlineData(KeyValuesFormat.Kv1, 0)]
        [InlineData(KeyValuesFormat.Kv2, 1)]
        [InlineData(KeyValuesFormat.Kv3, 2)]
        [InlineData(KeyValuesFormat.Auto, 3)]
        public void KeyValuesFormat_ValuesAndParsing_AreCorrect(KeyValuesFormat format, int expectedValue)
        {
            Assert.Equal(expectedValue, (int)format);

            bool success = Enum.TryParse(format.ToString(), out KeyValuesFormat result);
            Assert.True(success);
            Assert.Equal(format, result);
        }

        [Fact]
        public void KeyValuesFormat_Enum_CountIsCorrect()
        {
            Assert.Equal(4, Enum.GetValues<KeyValuesFormat>().Length);
        }
    }
}
