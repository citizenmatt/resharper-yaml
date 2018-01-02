using JetBrains.ReSharper.Psi.Parsing;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public static partial class YamlTokenType
  {
    public static readonly TokenNodeType BAD_CHARACTER = new GenericTokenNodeType("BAD_CHARACTER", LAST_GENERATED_TOKEN_TYPE_INDEX + 10, "�");

    public static readonly TokenNodeType EOF = new GenericTokenNodeType("EOF", LAST_GENERATED_TOKEN_TYPE_INDEX + 11, "EOF");

    public static readonly TokenNodeType NEW_LINE = new NewLineNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 12);
    public static readonly TokenNodeType WHITESPACE = new WhitespaceNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 13);
  }
}
