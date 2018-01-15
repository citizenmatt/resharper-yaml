using System;
using System.Text.RegularExpressions;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
{
  // A note about the contexts, as defined in the spec, because they are confusing.
  // The rules of the spec define contexts that control how multi-line scalars and
  // whitespace work. The problem is that they are not switches - you don't hit a
  // particular token and switch to another context. Instead, once you've matched
  // a rule, you're in the context that the rule is defined in.
  // For example, the spec starts in `block-in`. The next token might be part of a
  // block node (e.g. the indicator for a literal or folded block scalar), or it
  // might be from a flow node (e.g. single/double quotes, plain scalar, LBRACK or
  // LBRACE). The problem is that we've started in `block-in` and you can only
  // match a flow node when you're in `flow-out`, but there is no discrete switch
  // to get us into `flow-out`. By matching the flow node, we're ALREADY, implicitly
  // in `flow-out`. (We'd have to match either an alias, or tag properties, which
  // are defined as `flow-out` while we're still in `block-in`)
  // Another example is block mapping. We start in `block-in`. If we match QUEST,
  // we switch to `block-out` to match a block indented node. But this node might
  // be a flow node, such as a simple/implicit key. But that can only match if
  // we're in `flow-key` context. So there is no switch to `flow-key`. By matching
  // the construct, we're implicitly ALREADY in `flow-key`.
  // One more: if the first thing we match (in `block-in`) is a plain scalar, then
  // it could be a block mapping node with a simple/implicit key (which means we're
  // in `flow-key` context) or it could be a flow node with a plain salar, which
  // means we're in `flow-out` context. The only way to know what context we're
  // actually in is to continue lexing to see if we get a COLON or not.
  //
  // In other words, we don't swtich to a context and match, instead we match tokens
  // and that tells us what context we're in. Which means we can't map contexts to
  // lexer states, and that makes it very difficult to know if we're properly
  // following the spec.
  //
  // Phew.
  //
  // Fortunately, all is not lost. By studying the spec, the contexts mostly dictate
  // how we handle plain text - the `ns-plain` rule. It can be summed up:
  //
  // * `ns-plain(block-key)` = multi-line, any char
  // * `ns-plain(flow-in)` = multi-line, safe chars
  // * `ns-plain(flow-out)` = multi-line, any char
  // * `ns-plain(flow-key)` = single-line, safe chars
  //
  // As an overview, the spec starts in `block-in` and then:
  //
  // * Block scalars don't use `ns-plain`, handle themselves and stay in `block-in`
  // * Block sequence entries stay in `block-in` until they hit something more interesting
  // * Block sequence entry is MINUS followed by:
  //   * block-node (recursive, boring)
  //   * flow-node (see below)
  // * Block mapping
  //   * Explicit entry has key + value indicators. Key and value nodes are `block-out`
  //   * Implicit key (json) - boring
  //   * Implicit key (yaml) - `ns-plain(block-key)` (multi-line, any char)
  // * Flow scalar - `ns-plain(flow-out)` (multi-line, any char)
  // * Flow map entry key - `flow-in` (after LBRACE)
  //   * Explicit entry has key + value indicators. Key and value nodes are `flow-in` (multi-line, safe chars)
  //   * Implicit entry has value indicator, but not key. Key and value nodes are `flow-in` (multi-line, safe chars)
  // * Flow sequence entry - `flow-in` (after LBRACK)
  //   * Scalar entry - `ns-plain(flow-in)` (multi-line, safe char)
  //   * Flow-pair (compact notation)
  //     * Explicit - QUEST scalar (`ns-plain(flow-in)` - multi-line, safe char)
  //                  optional (COLON `ns-plain(flow-in)` - multi-line, safe char)
  //     * Implicit - `ns-plain(flow-key)` - single line, safe char
  //
  // All of which means we can simplify things. The safe char/any char thing is dependent
  // on being in a FLOW or BLOCK context. This is easily tracked with a level incremented
  // by LBRACK and LBRACE. Single line/multi-line is all about implicit key. We can summarise:
  //
  // If in BLOCK:
  //   Match (single line, any char) followed by COLON -> implicit block key
  //   Everything else is `flow-out` -> multi-line, any char
  // If in FLOW:
  //   Match (single line, safe char) followed by COLON -> implicit flow key
  //   Everything else is `flow-in` -> multi-line, safe char
  //
  // Furthermore, we can't match multi-line `ns-plain` in this lexer, as it requires a better
  // knowledge of the indent rules than we have - if the indent is less than or equal to the
  // initial indent, then it's a new token. We can't encode that in CsLex, so we have to match
  // INDENT tokens and single line tokens and fix it up in post production.
  // (We also have to handle the continuation line starting with any of the chars allowed in
  // `ns-plain-safe` that isn't allowed in `ns-plain-first`. I.e. `c-indicator` but not
  // `c-flow-indicator`)
  public partial class YamlLexerGenerated
  {
    // ReSharper disable InconsistentNaming
    private TokenNodeType currentTokenType;

    private int currentLineIndent;

    // The indent of the indicator of a block scalar. The contents must be more
    // indented than this. However, we also need to handle the case where the
    // indicator is at column 0, but we're in `block-in` context, i.e. at the
    // root of the doucment. This allows the indent to also be at column 0 (the
    // parent node is treated as being at column -1)
    // TODO: Use the indentation indicator value to set this
    private int blockScalarIndicatorIndent;
    private int blockScalarIndent;

    // The number of unclosed LBRACE and LBRACK. flowLevel == 0 means block context
    private int flowLevel = 0;

    private struct TokenPosition
    {
      public TokenNodeType CurrentTokenType;
      public int CurrentLineIndent;
      public int BlockScalarIndicatorIndent;
      public int BlockScalarIndent;
      public int FlowLevel;
      public int YyBufferIndex;
      public int YyBufferStart;
      public int YyBufferEnd;
      public int YyLexicalState;
    }
    // ReSharper restore InconsistentNaming

    public void Start()
    {
      Start(0, yy_buffer.Length, YYINITIAL);
    }

    public void Start(int startOffset, int endOffset, uint state)
    {
      yy_buffer_index = yy_buffer_start = yy_buffer_end = startOffset;
      yy_eof_pos = endOffset;
      yy_lexical_state = (int) state;
      currentTokenType = null;
    }

    public void Advance()
    {
      LocateToken();
      currentTokenType = null;
    }

    public object CurrentPosition
    {
      get
      {
        TokenPosition tokenPosition;
        tokenPosition.CurrentTokenType = currentTokenType;
        tokenPosition.CurrentLineIndent = currentLineIndent;
        tokenPosition.BlockScalarIndicatorIndent = blockScalarIndicatorIndent;
        tokenPosition.BlockScalarIndent = blockScalarIndent;
        tokenPosition.FlowLevel = flowLevel;
        tokenPosition.YyBufferIndex = yy_buffer_index;
        tokenPosition.YyBufferStart = yy_buffer_start;
        tokenPosition.YyBufferEnd = yy_buffer_end;
        tokenPosition.YyLexicalState = yy_lexical_state;
        return tokenPosition;
      }
      set
      {
        var tokenPosition = (TokenPosition) value;
        currentTokenType = tokenPosition.CurrentTokenType;
        currentLineIndent = tokenPosition.CurrentLineIndent;
        blockScalarIndicatorIndent = tokenPosition.BlockScalarIndicatorIndent;
        blockScalarIndent = tokenPosition.BlockScalarIndent;
        flowLevel = tokenPosition.FlowLevel;
        yy_buffer_index = tokenPosition.YyBufferIndex;
        yy_buffer_start = tokenPosition.YyBufferStart;
        yy_buffer_end = tokenPosition.YyBufferEnd;
        yy_lexical_state = tokenPosition.YyLexicalState;
      }
    }

    public TokenNodeType TokenType => LocateToken();

    public int TokenStart
    {
      get
      {
        LocateToken();
        return yy_buffer_start;
      }
    }

    public int TokenEnd
    {
      get
      {
        LocateToken();
        return yy_buffer_end;
      }
    }

    public IBuffer Buffer => yy_buffer;
    public uint LexerStateEx => (uint) yy_lexical_state;

    public int EOFPos => yy_eof_pos;
    public int LexemIndent => 7;  // No, I don't know why

    private bool IsBlock => flowLevel == 0;

    protected void RewindToken()
    {
      yy_buffer_end = yy_buffer_index = yy_buffer_start;
    }

    private TokenNodeType LocateToken()
    {
      if (currentTokenType == null)
      {
        try
        {
          currentTokenType = _locateToken();
        }
        catch (Exception e)
        {
          e.AddData("TokenType", () => currentTokenType);
          e.AddData("LexerState", () => LexerStateEx);
          e.AddData("TokenStart", () => yy_buffer_start);
          e.AddData("TokenPos", () => yy_buffer_index);
          e.AddData("Buffer", () =>
          {
            var start = Math.Max(0, yy_buffer_end);
            var tokenText = yy_buffer.GetText(new TextRange(start, yy_buffer_index));
            tokenText = Regex.Replace(tokenText, @"\p{Cc}", a => $"[{(byte) a.Value[0]:X2}]");
            return tokenText;
          });
          throw;
        }
      }

      return currentTokenType;
    }

    protected void RewindChar()
    {
      yy_buffer_end = yy_buffer_index = yy_buffer_index - 1;
    }

    protected void RewindWhitespace()
    {
      while (yy_buffer_index > 0 && IsWhitespace(yy_buffer[yy_buffer_index - 1]))
        yy_buffer_end = yy_buffer_index = yy_buffer_index - 1;
    }

    private static bool IsWhitespace(char c) => c == ' ' || c == '\t';

    private void PushFlowIndicator()
    {
      flowLevel++;
      yybegin(FLOW);
    }

    private void PopFlowIndicator()
    {
      flowLevel = Math.Max(0, flowLevel - 1);
      if (IsBlock)
        yybegin(BLOCK);
    }

    private void ResetBlockFlowState()
    {
      yybegin(IsBlock ? BLOCK : FLOW);
    }

    private void BeginBlockScalar()
    {
      blockScalarIndicatorIndent = currentLineIndent;
      blockScalarIndent = -1;
      yybegin(BLOCK_SCALAR_HEADER);
    }

    private void HandleBlockScalarWhitespace()
    {
      // If the content indent hasn't been set, and we're indented in relation to the
      // indicator, indent the content, otherwise, terminate the block scalar
      if (blockScalarIndent == -1 && currentLineIndent >= blockScalarIndicatorIndent)
        blockScalarIndent = currentLineIndent;
      else if (currentLineIndent <= blockScalarIndent)
        ResetBlockFlowState();
    }

    private TokenNodeType HandleBlockScalarLine()
    {
      if (currentLineIndent <= blockScalarIndent)
      {
        EndBlockScalar();
        RewindToken();
        return _locateToken();
      }

      return YamlTokenType.SCALAR_TEXT;
    }

    private void EndBlockScalar()
    {
      blockScalarIndent = 0;
      ResetBlockFlowState();
    }
  }
}