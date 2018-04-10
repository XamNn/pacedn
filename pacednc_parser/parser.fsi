// Signature file for parser generated by fsyacc
module Pace.Compiler.Parsing.Parser
type token = 
  | EOF
  | GLOBAL
  | MARKUP
  | ELEMENT
  | CLASS
  | STRUCT
  | RETURN
  | NULL
  | OBJECT
  | GET
  | SET
  | ALIAS
  | IF
  | WHEN
  | THEN
  | ELSE
  | BREAK
  | CONTINUE
  | IS
  | IMPORT
  | CONFIG
  | MAIN
  | FUNC
  | PUBLIC
  | PRIVATE
  | VISIBLE
  | THIS
  | INIT
  | FOR
  | YIELD
  | IMPLICIT
  | AUTOMATIC
  | OPERATOR
  | TRUE
  | FALSE
  | NOT
  | AND
  | OR
  | PERIOD
  | SEMICOLON
  | COMMA
  | EQUALS
  | LAMBDA
  | AT
  | QUESTION
  | PARENOPEN
  | PARENCLOSE
  | SQUAREOPEN
  | SQUARECLOSE
  | CURLYOPEN
  | CURLYCLOSE
  | ANGLEOPEN
  | ANGLECLOSE
  | ID of (string)
type tokenId = 
    | TOKEN_EOF
    | TOKEN_GLOBAL
    | TOKEN_MARKUP
    | TOKEN_ELEMENT
    | TOKEN_CLASS
    | TOKEN_STRUCT
    | TOKEN_RETURN
    | TOKEN_NULL
    | TOKEN_OBJECT
    | TOKEN_GET
    | TOKEN_SET
    | TOKEN_ALIAS
    | TOKEN_IF
    | TOKEN_WHEN
    | TOKEN_THEN
    | TOKEN_ELSE
    | TOKEN_BREAK
    | TOKEN_CONTINUE
    | TOKEN_IS
    | TOKEN_IMPORT
    | TOKEN_CONFIG
    | TOKEN_MAIN
    | TOKEN_FUNC
    | TOKEN_PUBLIC
    | TOKEN_PRIVATE
    | TOKEN_VISIBLE
    | TOKEN_THIS
    | TOKEN_INIT
    | TOKEN_FOR
    | TOKEN_YIELD
    | TOKEN_IMPLICIT
    | TOKEN_AUTOMATIC
    | TOKEN_OPERATOR
    | TOKEN_TRUE
    | TOKEN_FALSE
    | TOKEN_NOT
    | TOKEN_AND
    | TOKEN_OR
    | TOKEN_PERIOD
    | TOKEN_SEMICOLON
    | TOKEN_COMMA
    | TOKEN_EQUALS
    | TOKEN_LAMBDA
    | TOKEN_AT
    | TOKEN_QUESTION
    | TOKEN_PARENOPEN
    | TOKEN_PARENCLOSE
    | TOKEN_SQUAREOPEN
    | TOKEN_SQUARECLOSE
    | TOKEN_CURLYOPEN
    | TOKEN_CURLYCLOSE
    | TOKEN_ANGLEOPEN
    | TOKEN_ANGLECLOSE
    | TOKEN_ID
    | TOKEN_end_of_input
    | TOKEN_error
type nonTerminalId = 
    | NONTERM__startstart
    | NONTERM_start
/// This function maps tokens to integer indexes
val tagOfToken: token -> int

/// This function maps integer indexes to symbolic token ids
val tokenTagToTokenId: int -> tokenId

/// This function maps production indexes returned in syntax errors to strings representing the non terminal that would be produced by that production
val prodIdxToNonTerminal: int -> nonTerminalId

/// This function gets the name of a token as a string
val token_to_string: token -> string
val start : (Microsoft.FSharp.Text.Lexing.LexBuffer<'cty> -> token) -> Microsoft.FSharp.Text.Lexing.LexBuffer<'cty> -> ( string ) 