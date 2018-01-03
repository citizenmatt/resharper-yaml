using JetBrains.ReSharper.Psi.Parsing;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public static partial class YamlTokenType
  {
    public static readonly TokenNodeType BAD_CHARACTER = new GenericTokenNodeType("BAD_CHARACTER", LAST_GENERATED_TOKEN_TYPE_INDEX + 10, "�");

    public static readonly TokenNodeType EOF = new GenericTokenNodeType("EOF", LAST_GENERATED_TOKEN_TYPE_INDEX + 11, "EOF");

    public static readonly TokenNodeType NEW_LINE = new NewLineNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 12);
    public static readonly TokenNodeType WHITESPACE = new WhitespaceNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 13);

    public static readonly TokenNodeType COMMENT = new CommentTokenNodeType(LAST_GENERATED_TOKEN_TYPE_INDEX + 14);

    // TODO: Naming. The YAML spec has an interesting hungarian notation style...
    public static readonly TokenNodeType IDENTIFIER = new GenericTokenNodeType("IDENTIFIER", LAST_GENERATED_TOKEN_TYPE_INDEX + 20, "IDENTIFIER");
    public static readonly TokenNodeType NS_PLAIN = new GenericTokenNodeType("NS_PLAIN", LAST_GENERATED_TOKEN_TYPE_INDEX + 21, "NS_PLAIN");
    public static readonly TokenNodeType NS_ANCHOR_NAME = new GenericTokenNodeType("NS_ANCHOR_NAME", LAST_GENERATED_TOKEN_TYPE_INDEX + 22, "NS_ANCHOR_NAME");
    public static readonly TokenNodeType C_SINGLE_QUOTED = new GenericTokenNodeType("C_SINGLE_QUOTED", LAST_GENERATED_TOKEN_TYPE_INDEX + 23, "C_SINGLE_QUOTED");
  }
}
