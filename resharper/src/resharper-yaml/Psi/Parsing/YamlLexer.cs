using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public class YamlLexer : YamlLexerGenerated
  {
    public YamlLexer(IBuffer buffer)
      : base(buffer)
    {
    }

    public YamlLexer(IBuffer buffer, int startOffset, int endOffset)
      : base(buffer, startOffset, endOffset)
    {
    }

    public override TokenNodeType _locateToken()
    {
      var token = base._locateToken();

      if (token == YamlTokenType.SYNTHETIC_DIRECTIVES_END)
      {
        // We only get this while trying to lex directives (YYINITIAL). We've
        // just found a char we don't know. The catch all rule has already
        // switched us to BLOCK_IN, so we just rewind the char. The next time
        // _locateToken is called, we'll start to lex the document content
        RewindToken();
      }
      else if (token == YamlTokenType._INTERNAL_BLOCK_KEY || token == YamlTokenType._INTERNAL_FLOW_KEY)
      {
        // Remove the trailing COLON and whitespace
        RewindChar();
        RewindWhitespace();
        return YamlTokenType.NS_PLAIN_ONE_LINE;
      }

      return token;
    }
  }
}