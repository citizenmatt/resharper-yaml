using System;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.Util;

%%

%unicode

%init{
  currentTokenType = null;
  currentLineIndent = 0;
%init}

%namespace JetBrains.ReSharper.Plugins.Yaml.Psi.Parsing
%class YamlLexerGenerated
%implements IIncrementalLexer
%function _locateToken
%virtual
%public
%type TokenNodeType
%ignorecase

%eofval{
  currentTokenType = null; return currentTokenType;
%eofval}

%include Chars.lex

APOS_CHAR=\u0027
BACKSLASH_CHAR=\u005C
CARET_CHAR=\u005E
DOT_CHAR=\u002E
MINUS_CHAR=\u002D
LBRACK_CHAR=\u005B
RBRACK_CHAR=\u005D
LBRACE_CHAR=\u007B
RBRACE_CHAR=\u007D
QUOTE_CHAR=\u0022


YAML11_NEW_LINE_CHARS={LINE_SEPARATOR}{PARAGRAPH_SEPARATOR}
NEW_LINE_CHARS={CR}{LF}
NEW_LINE=({CR}?{LF}|{CR}|{LINE_SEPARATOR}|{PARAGRAPH_SEPARATOR})
NOT_NEW_LINE=([^{NEW_LINE_CHARS}{YAML11_NEW_LINE_CHARS}])

INPUT_CHARACTER={NOT_NEW_LINE}

WHITESPACE_CHARS={SP}{TAB}
WHITESPACE=[{WHITESPACE_CHARS}]+
OPTIONAL_WHITESPACE=[{WHITESPACE_CHARS}]*

ASCII_COMMON=[0-9a-zA-Z]
ASCII_SYMBOLS=[\u0021-\u002F\u003A-\u0040\u005B-\u0060\u007B-\u007E]
ASCII_PRINTABLE={ASCII_COMMON}|{ASCII_SYMBOLS}
OTHER_PRINTABLE={NEL}|[\u00A0-\uD7FF]|[\uE000-\uFFFD]|[\u10000-\10FFFF]

C_PRINTABLE={TAB}|{SP}|{CR}|{LF}|{ASCII_PRINTABLE}|{OTHER_PRINTABLE}
C_INDICATOR=[{MINUS_CHAR}?:,{LBRACK_CHAR}{RBRACK_CHAR}{LBRACE_CHAR}{RBRACE_CHAR}#&*!|>'{QUOTE_CHAR}%@`]
C_FLOW_INDICATOR=[,{LBRACK_CHAR}{RBRACK_CHAR}{LBRACE_CHAR}{RBRACE_CHAR}]

NB_JSON=({TAB}|[\u0020-\u10FFFF])
NB_CHAR=({TAB}|{SP}|{ASCII_PRINTABLE}|{OTHER_PRINTABLE})
NS_CHAR=({ASCII_PRINTABLE}|{OTHER_PRINTABLE})

NB_JSON_MINUS_SINGLE_QUOTE=({TAB}|[\u0020-\u0026\u0028-\u10FFFF])
NB_JSON_MINUS_DOUBLE_QUOTE=({TAB}|[\u0020-\u0021\u0023-\u10FFFF])
NS_CHAR_MINUS_C_INDICATOR=({ASCII_COMMON}|{OTHER_PRINTABLE}|[$()+{DOT_CHAR}/;<={BACKSLASH_CHAR}{CARET_CHAR}_])
NS_CHAR_MINUS_C_FLOW_INDICATOR=({ASCII_COMMON}|{OTHER_PRINTABLE}|[!{QUOTE_CHAR}#$%&'()*+{MINUS_CHAR}{DOT_CHAR}/:;<=>?@{BACKSLASH_CHAR}{CARET_CHAR}_`|~])

URI_SYMBOLS=[#;/?:@&=+$,_{DOT_CHAR}!~*'(){LBRACK_CHAR}{RBRACK_CHAR}]
URI_SYMBOLS_MINUS_BANG_AND_C_FLOW_INDICATOR=[#;/?:@&=+$_{DOT_CHAR}~*'()]

NS_DEC_DIGIT=[0-9]
NS_HEX_DIGIT=[0-9a-fA-F]
NS_ASCII_LETTER=[a-zA-Z]

NS_WORD_CHAR=({NS_DEC_DIGIT}|{NS_ASCII_LETTER}|"-")

URL_ENCODED_CHAR=("%"{NS_HEX_DIGIT}{NS_HEX_DIGIT})
NS_URI_CHAR=({URL_ENCODED_CHAR}|{NS_WORD_CHAR}|{URI_SYMBOLS})
NS_TAG_CHAR=({URL_ENCODED_CHAR}|{NS_WORD_CHAR}|{URI_SYMBOLS_MINUS_BANG_AND_C_FLOW_INDICATOR})

NS_PLAIN_SAFE_IN={NS_CHAR_MINUS_C_FLOW_INDICATOR}
NS_PLAIN_SAFE_OUT={NS_CHAR}

NS_PLAIN_SAFE_OUT_MINUS_COLON_AND_HASH=({ASCII_COMMON}|{OTHER_PRINTABLE}|[\u0021\u0022\u0024-\u002F\u003B-\u0040\u005B-\u0060\u007B-\u007E])
NS_PLAIN_SAFE_IN_MINUS_COLON_AND_HASH=({ASCII_COMMON}|{OTHER_PRINTABLE}|[!{QUOTE_CHAR}$%&'()*+{MINUS_CHAR}{DOT_CHAR}/;<=>?@{BACKSLASH_CHAR}{CARET_CHAR}_`|~])

NS_PLAIN_SAFE={NS_PLAIN_SAFE_IN}
NS_PLAIN_SAFE_MINUS_COLON_AND_HASH={NS_PLAIN_SAFE_IN_MINUS_COLON_AND_HASH}

NS_PLAIN_CHAR=({NS_PLAIN_SAFE_MINUS_COLON_AND_HASH}|({NS_CHAR}"#")|(":"{NS_PLAIN_SAFE}))
NS_PLAIN_FIRST=({NS_CHAR_MINUS_C_INDICATOR}|([?:-]{NS_PLAIN_SAFE}))
NB_NS_PLAIN_IN_LINE=({OPTIONAL_WHITESPACE}{NS_PLAIN_CHAR})*
NS_PLAIN_ONE_LINE={NS_PLAIN_FIRST}{NB_NS_PLAIN_IN_LINE}

NS_ANCHOR_CHAR=({NS_CHAR_MINUS_C_FLOW_INDICATOR})
NS_ANCHOR_NAME={NS_ANCHOR_CHAR}+

C_QUOTED_QUOTE=({APOS_CHAR}{APOS_CHAR})
NB_SINGLE_CHAR=({C_QUOTED_QUOTE}|{NB_JSON_MINUS_SINGLE_QUOTE})
NB_SINGLE_ONE_LINE={NB_SINGLE_CHAR}*
NB_SINGLE_MULTI_LINE=(C_QUOTED_QUOTE}|{NB_JSON_MINUS_SINGLE_QUOTE}|{NEW_LINE})*
C_SINGLE_QUOTED_key={APOS_CHAR}{NB_SINGLE_ONE_LINE}{APOS_CHAR}
C_SINGLE_QUOTED_flow={APOS_CHAR}{NB_SINGLE_MULTI_LINE}{APOS_CHAR}

NB_DOUBLE_CHAR=({NB_JSON_MINUS_DOUBLE_QUOTE}|{BACKSLASH_CHAR}{QUOTE_CHAR})
NB_DOUBLE_ONE_LINE={NB_DOUBLE_CHAR}*
NB_DOUBLE_MULTI_LINE=({NB_JSON_MINUS_DOUBLE_QUOTE}|{BACKSLASH_CHAR}{QUOTE_CHAR}|{NEW_LINE})*
C_DOUBLE_QUOTED_key={QUOTE_CHAR}{NB_DOUBLE_ONE_LINE}{QUOTE_CHAR}
C_DOUBLE_QUOTED_flow={QUOTE_CHAR}{NB_DOUBLE_MULTI_LINE}{QUOTE_CHAR}

C_NB_COMMENT_TEXT="#"{NB_CHAR}*

C_DIRECTIVES_END=^"---"
C_DOCUMENT_END=^"..."


%state BLOCK_IN, BLOCK_OUT, BLOCK_KEY
%state FLOW_IN, FLOW_OUT, FLOW_KEY
%state DIRECTIVE
%state BLOCK_SCALAR_HEADER, BLOCK_SCALAR
%state ANCHOR_ALIAS
%state SHORTHAND_TAG, VERBATIM_TAG

%%

<YYINITIAL, BLOCK_IN>
                ^{WHITESPACE}           { currentLineIndent = yy_buffer_end; return YamlTokenType.INDENT; }
<YYINITIAL, BLOCK_IN>
                {WHITESPACE}            { return YamlTokenType.WHITESPACE; }
<YYINITIAL, BLOCK_IN>
                {NEW_LINE}              { currentLineIndent = 0; return YamlTokenType.NEW_LINE; }

<YYINITIAL>     ^"%"                    { yybegin(DIRECTIVE); return YamlTokenType.PERCENT; }
<YYINITIAL>     {C_DIRECTIVES_END}      { currentLineIndent = 0; yybegin(BLOCK_IN); return YamlTokenType.DIRECTIVES_END; }
<YYINITIAL>     .                       { currentLineIndent = 0; yybegin(BLOCK_IN); return YamlTokenType.SYNTHETIC_DIRECTIVES_END; }

<YYINITIAL, BLOCK_IN, BLOCK_SCALAR>
                {C_DOCUMENT_END}        { yybegin(YYINITIAL); return YamlTokenType.DOCUMENT_END; }


<BLOCK_IN>      {C_DIRECTIVES_END}      { return YamlTokenType.DIRECTIVES_END; }
<BLOCK_IN>      {NS_PLAIN_ONE_LINE}     { return YamlTokenType.NS_PLAIN; }

<BLOCK_IN>      "&"                     { yybegin(ANCHOR_ALIAS); return YamlTokenType.AMP; }
<BLOCK_IN>      "*"                     { yybegin(ANCHOR_ALIAS); return YamlTokenType.ASTERISK; }
<BLOCK_IN>      "!"                     { yybegin(SHORTHAND_TAG); return YamlTokenType.BANG; }
<BLOCK_IN>      "!<"                    { yybegin(VERBATIM_TAG); return YamlTokenType.BANG_LT; }
<BLOCK_IN>      ">"                     { BeginBlockScalar(); return YamlTokenType.GT; }
<BLOCK_IN>      "|"                     { BeginBlockScalar(); return YamlTokenType.PIPE; }
<BLOCK_IN>      ":"                     { return YamlTokenType.COLON; }
<BLOCK_IN>      ","                     { return YamlTokenType.COMMA; }
<BLOCK_IN>      "-"                     { return YamlTokenType.MINUS; }
<BLOCK_IN>      "<"                     { return YamlTokenType.LT; }
<BLOCK_IN>      "{"                     { return YamlTokenType.LBRACE; }
<BLOCK_IN>      "}"                     { return YamlTokenType.RBRACE; }
<BLOCK_IN>      "["                     { return YamlTokenType.LBRACK; }
<BLOCK_IN>      "]"                     { return YamlTokenType.RBRACK; }
<BLOCK_IN>      "%"                     { return YamlTokenType.PERCENT; }
<BLOCK_IN>      "?"                     { return YamlTokenType.QUESTION; }

<YYINITIAL, DIRECTIVE, BLOCK_SCALAR_HEADER, BLOCK_IN>
                {C_NB_COMMENT_TEXT}     { return YamlTokenType.COMMENT; }

<BLOCK_IN>      {C_SINGLE_QUOTED_key}   { return YamlTokenType.C_SINGLE_QUOTED_SINGLE_LINE; }
<BLOCK_IN>      {C_SINGLE_QUOTED_flow}  { return YamlTokenType.C_SINGLE_QUOTED_MULTILINE; }
<BLOCK_IN>      {C_DOUBLE_QUOTED_key}   { return YamlTokenType.C_DOUBLE_QUOTED_SINGLE_LINE; }
<BLOCK_IN>      {C_DOUBLE_QUOTED_flow}  { return YamlTokenType.C_DOUBLE_QUOTED_MULTILINE; }
<BLOCK_IN>      {NS_PLAIN_ONE_LINE}     { return YamlTokenType.NS_PLAIN; }


<DIRECTIVE>     {WHITESPACE}            { return YamlTokenType.WHITESPACE; }
<DIRECTIVE>     {NEW_LINE}              { currentLineIndent = 0; yybegin(YYINITIAL); return YamlTokenType.NEW_LINE; }
<DIRECTIVE>     "!"                     { return YamlTokenType.BANG; }
<DIRECTIVE>     {NS_CHAR}+              { return YamlTokenType.NS_CHARS; }
<DIRECTIVE>     {NS_WORD_CHAR}+         { return YamlTokenType.NS_WORD_CHARS; }
<DIRECTIVE>     {NS_URI_CHAR}+          { return YamlTokenType.NS_URI_CHARS; }


<BLOCK_SCALAR_HEADER>
                {NEW_LINE}              { currentLineIndent = 0; yybegin(BLOCK_SCALAR); return YamlTokenType.NEW_LINE; }
<BLOCK_SCALAR_HEADER>
                "+"                     { return YamlTokenType.PLUS; }
<BLOCK_SCALAR_HEADER>
                "-"                     { return YamlTokenType.MINUS; }
<BLOCK_SCALAR_HEADER>
                {NS_DEC_DIGIT}          { return YamlTokenType.NS_DEC_DIGIT; }
<BLOCK_SCALAR_HEADER>
                {WHITESPACE}            { return YamlTokenType.WHITESPACE; }
                
<BLOCK_SCALAR>  {NEW_LINE}              { currentLineIndent = 0; return YamlTokenType.NEW_LINE; }
<BLOCK_SCALAR>  ^{WHITESPACE}           { currentLineIndent = yy_buffer_end; HandleBlockScalarWhitespace(); return YamlTokenType.INDENT; } 
<BLOCK_SCALAR>  {WHITESPACE}            { HandleBlockScalarWhitespace(); return YamlTokenType.WHITESPACE; }
<BLOCK_SCALAR>  {NB_CHAR}+              { return YamlTokenType.SCALAR_TEXT; }
<BLOCK_SCALAR>  ^([^{WHITESPACE_CHARS}]){NB_CHAR}+
                                        { return HandleBlockScalarLine(); }


<ANCHOR_ALIAS>  {WHITESPACE}            { yybegin(BLOCK_IN); return YamlTokenType.WHITESPACE; }
<ANCHOR_ALIAS>  {NEW_LINE}              { currentLineIndent = 0; yybegin(BLOCK_IN); return YamlTokenType.NEW_LINE; }
<ANCHOR_ALIAS>  {NS_ANCHOR_NAME}        { yybegin(BLOCK_IN); return YamlTokenType.NS_ANCHOR_NAME; }


<SHORTHAND_TAG, VERBATIM_TAG>
                {WHITESPACE}            { yybegin(BLOCK_IN); return YamlTokenType.WHITESPACE; }
<SHORTHAND_TAG, VERBATIM_TAG>
                {NEW_LINE}              { currentLineIndent = 0; yybegin(BLOCK_IN); return YamlTokenType.NEW_LINE; }

<SHORTHAND_TAG> "!"                     { return YamlTokenType.BANG; }
<SHORTHAND_TAG> {NS_TAG_CHAR}+          { yybegin(BLOCK_IN); return YamlTokenType.NS_TAG_CHARS; }


<VERBATIM_TAG>  {NEW_LINE}              { currentLineIndent = 0; yybegin(BLOCK_IN); return YamlTokenType.NEW_LINE; }
<VERBATIM_TAG>  {NS_URI_CHAR}+          { return YamlTokenType.NS_URI_CHARS; }
<VERBATIM_TAG>  ">"                     { yybegin(BLOCK_IN); return YamlTokenType.GT; }


<DIRECTIVE,BLOCK_IN,BLOCK_OUT,BLOCK_KEY,FLOW_IN,FLOW_OUT,FLOW_KEY,ANCHOR_ALIAS,SHORTHAND_TAG,VERBATIM_TAG>
                .                       { return YamlTokenType.BAD_CHARACTER; }