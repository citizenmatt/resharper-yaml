using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  public static class ParserMessages
  {
    public const string IDS_NODE = "node";
    public const string IDS_BLOCK_HEADER = "block header";
    public const string IDS_BLOCK_NODE = "block node";
    public const string IDS_BLOCK_SCALAR_NODE = "block scalar node";
    public const string IDS_CHOMPING_INDICATOR = "chomping indicator";
    public const string IDS_DIRECTIVE = "directive";
    public const string IDS_DOUBLE_QUOTED_SCALAR_NODE = "double quoted scalar";
    public const string IDS_FLOW_IN_BLOCK_NODE = "flow in block node";
    public const string IDS_FLOW_NODE = "flow node";
    public const string IDS_FOLDED_SCALAR_NODE = "folded scalar";
    public const string IDS_LITERAL_SCALAR_NODE = "literal scalar";
    public const string IDS_PLAIN_SCALAR_NODE = "plain scalar";
    public const string IDS_SINGLE_QUOTED_SCALAR_NODE = "single quoted scalar";

    public static string GetString(string id) => id;

    public static string GetUnexpectedTokenMessage() => "Unexpected token";

    public static string GetExpectedMessage(string expectedSymbol)
    {
      return string.Format(GetString("{0} expected"), expectedSymbol).Capitalize();
    }
  }
}