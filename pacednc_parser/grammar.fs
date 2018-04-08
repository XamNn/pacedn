module Pace.Compiler
open Pace.Compiler.Parsing.Lexer
open Microsoft.FSharp.Text.Lexing

let Process string = tokenize (LexBuffer<char>.FromString string)