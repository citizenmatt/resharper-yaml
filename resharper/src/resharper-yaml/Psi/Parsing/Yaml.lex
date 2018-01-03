using System;
using JetBrains.ReSharper.Psi.Parsing;
using JetBrains.Text;
using JetBrains.Util;

%%

%unicode

%init{
  currentTokenType = null;
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

NEW_LINE_CHARS={CR}{LF}
NEW_LINE=({CR}?{LF}|{CR})
NOT_NEW_LINE=([^{NEW_LINE_CHARS}])

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
NS_TAG_CHAR={URL_ENCODED_CHAR}|{NS_WORD_CHAR}|{URI_SYMBOLS_MINUS_BANG_AND_C_FLOW_INDICATOR}

NS_PLAIN_SAFE_IN={NS_CHAR_MINUS_C_FLOW_INDICATOR}
NS_PLAIN_SAFE_OUT={NS_CHAR}

NS_PLAIN_SAFE_OUT_MINUS_COLON_AND_HASH=({ASCII_COMMON}|{OTHER_PRINTABLE}|[\u0021\u0022\u0024-\u002F\u003B-\u0040\u005B-\u0060\u007B-\u007E])
NS_PLAIN_SAFE_IN_MINUS_COLON_AND_HASH=({ASCII_COMMON}|{OTHER_PRINTABLE}|[!{QUOTE_CHAR}$%&'()*+{MINUS_CHAR}{DOT_CHAR}/;<=>?@{BACKSLASH_CHAR}{CARET_CHAR}_`|~])

NS_PLAIN_SAFE={NS_PLAIN_SAFE_IN}
NS_PLAIN_SAFE_MINUS_COLON_AND_HASH={NS_PLAIN_SAFE_IN_MINUS_COLON_AND_HASH}

NS_PLAIN_CHAR=({NS_PLAIN_SAFE_MINUS_COLON_AND_HASH}|({NS_CHAR}"#")|(":"{NS_PLAIN_SAFE}))
NS_PLAIN_FIRST=({NS_CHAR_MINUS_C_INDICATOR}|(("?"|":"|"-"){NS_PLAIN_SAFE}))
NB_NS_PLAIN_IN_LINE=({OPTIONAL_WHITESPACE}{NS_PLAIN_CHAR})*
NS_PLAIN_ONE_LINE={NS_PLAIN_FIRST}{NB_NS_PLAIN_IN_LINE}

NS_ANCHOR_CHAR=({NS_CHAR_MINUS_C_FLOW_INDICATOR})
NS_ANCHOR_NAME={NS_ANCHOR_CHAR}+

C_QUOTED_QUOTE=({APOS_CHAR}{APOS_CHAR})
NB_SINGLE_CHAR=({C_QUOTED_QUOTE}|{NB_JSON_MINUS_SINGLE_QUOTE})
NB_SINGLE_ONE_LINE={NB_SINGLE_CHAR}*
NB_SINGLE_TEXT={NB_SINGLE_ONE_LINE}
C_SINGLE_QUOTED={APOS_CHAR}{NB_SINGLE_TEXT}{APOS_CHAR}

C_NB_COMMENT_TEXT="#"{NB_CHAR}*


%state ANCHOR_ALIAS
%state SHORTHAND_TAG

%%

<YYINITIAL>     {WHITESPACE}          { return YamlTokenType.WHITESPACE; }
<YYINITIAL>     {NEW_LINE}            { return YamlTokenType.NEW_LINE; }

<YYINITIAL>     "&"                   { yybegin(ANCHOR_ALIAS); return YamlTokenType.AMP; }
<YYINITIAL>     "*"                   { yybegin(ANCHOR_ALIAS); return YamlTokenType.ASTERISK; }
<YYINITIAL>     "!"                   { yybegin(SHORTHAND_TAG); return YamlTokenType.BANG; }
<YYINITIAL>     ":"                   { return YamlTokenType.COLON; }
<YYINITIAL>     ","                   { return YamlTokenType.COMMA; }
<YYINITIAL>     "-"                   { return YamlTokenType.MINUS; }
<YYINITIAL>     "<"                   { return YamlTokenType.LT; }
<YYINITIAL>     ">"                   { return YamlTokenType.GT; }
<YYINITIAL>     "{"                   { return YamlTokenType.LBRACE; }
<YYINITIAL>     "}"                   { return YamlTokenType.RBRACE; }
<YYINITIAL>     "["                   { return YamlTokenType.LBRACK; }
<YYINITIAL>     "]"                   { return YamlTokenType.RBRACK; }

<YYINITIAL>     {C_NB_COMMENT_TEXT}   { return YamlTokenType.COMMENT; }
<YYINITIAL>     {C_SINGLE_QUOTED}     { return YamlTokenType.C_SINGLE_QUOTED; }
<YYINITIAL>     {NS_PLAIN_ONE_LINE}   { return YamlTokenType.NS_PLAIN; }

<ANCHOR_ALIAS>  {WHITESPACE}          { yybegin(YYINITIAL); return YamlTokenType.WHITESPACE; }
<ANCHOR_ALIAS>  {NEW_LINE}            { yybegin(YYINITIAL); return YamlTokenType.NEW_LINE; }
<ANCHOR_ALIAS>  {NS_ANCHOR_NAME}      { yybegin(YYINITIAL); return YamlTokenType.NS_ANCHOR_NAME; }

<SHORTHAND_TAG> {WHITESPACE}          { yybegin(YYINITIAL); return YamlTokenType.WHITESPACE; }
<SHORTHAND_TAG> {NEW_LINE}            { yybegin(YYINITIAL); return YamlTokenType.NEW_LINE; }
<SHORTHAND_TAG> "!"                   { return YamlTokenType.BANG; }
<SHORTHAND_TAG> "<"                   { return YamlTokenType.LT; }
<SHORTHAND_TAG> ">"                   { return YamlTokenType.GT; }
<SHORTHAND_TAG> ({NS_TAG_CHAR})+      { yybegin(YYINITIAL); return YamlTokenType.IDENTIFIER; }

<YYINITIAL,ANCHOR_ALIAS,SHORTHAND_TAG>
                .                     { return YamlTokenType.BAD_CHARACTER; }