[<AutoOpen>]
module internal SurrealDB.Client.FSharp.Preamble

open System
open System.Collections.Generic
open System.Globalization
open System.Text
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
module ValueOption =
    let ofOption = function
        | Some x -> ValueSome x
        | None -> ValueNone

[<RequireQualifiedAccess>]
module String =
    let inline isEmpty (s: string) = String.IsNullOrEmpty s
    let inline isWhiteSpace (s: string) = String.IsNullOrWhiteSpace s

    let inline internal toBase64 (s: string) =
        Convert.ToBase64String(Encoding.UTF8.GetBytes(s))

    let orEmpty s = if isNull s then "" else s

[<RequireQualifiedAccess>]
module Double =
    let tryParse (s: string) =
        match Double.TryParse(s, NumberStyles.Currency, CultureInfo.InvariantCulture) with
        | true, date -> ValueSome date
        | false, _ -> ValueNone

[<RequireQualifiedAccess>]
module DateTimeOffset =
    let tryParse (s: string) =
        match DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None) with
        | true, date -> ValueSome date
        | false, _ -> ValueNone

[<RequireQualifiedAccess>]
module TimeSpan =
    let regex =
        Regex(@"^(?<amount>\d+(\.\d+)?)(?<unit>s|ms|µs|ns)$", RegexOptions.Compiled ||| RegexOptions.IgnoreCase)

    let internal unitsToSeconds unit' =
        match unit' with
        | "s" -> ValueSome 1.0
        | "ms" -> ValueSome 1e-3
        | "µs" -> ValueSome 1e-6
        | "ns" -> ValueSome 1e-9
        | _ -> ValueNone

    let internal fromMatch (match': Match) =
        let amount =
            Double.tryParse (match'.Groups.["amount"].Value)

        let seconds =
            unitsToSeconds (match'.Groups.["unit"].Value)

        match amount, seconds with
        | ValueSome amount, ValueSome seconds -> ValueSome(TimeSpan.FromSeconds(amount * seconds))
        | _ -> ValueNone

    let tryParse s =
        if String.isWhiteSpace s then
            ValueNone
        else
            ValueSome s
        |> ValueOption.map (fun s -> regex.Match(s))
        |> ValueOption.bind (fun match' ->
            if match'.Success then
                ValueSome match'
            else
                ValueNone)
        |> ValueOption.bind fromMatch

[<RequireQualifiedAccess>]
module Seq =
    let inline getEnumerator (source: seq<'T>) = source.GetEnumerator()
    let inline moveNext (enumerator: IEnumerator<'T>) = enumerator.MoveNext()
    let inline getCurrent (enumerator: IEnumerator<'T>) = enumerator.Current

    let tryHeadValue source =
        use enumerator = getEnumerator source

        if moveNext enumerator then
            ValueSome(getCurrent enumerator)
        else
            ValueNone

[<RequireQualifiedAccess>]
module Array =
    let tryHeadValue (source: 'a array) =
        if source.Length > 0 then
            ValueSome source.[0]
        else
            ValueNone

[<RequireQualifiedAccess>]
module Json =
    open System.Text.Json
    open System.Text.Json.Nodes

    let defaultOptions =
        let o = JsonSerializerOptions()
        // o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        o

    let deserialize<'a> (json: string) =
        JsonSerializer.Deserialize<'a>(json, defaultOptions)

    let serialize<'a> (data: 'a) =
        JsonSerializer.Serialize<'a>(data, defaultOptions)

    let deserializeNode<'a> (json: JsonNode) =
        json.Deserialize<'a>(defaultOptions)

    let serializeNode<'a> (data: 'a) =
        JsonSerializer.SerializeToNode<'a>(data, defaultOptions)

[<RequireQualifiedAccess>]
module Task =
    open System.Threading.Tasks

    let map (f: 'a -> 'b) (ma: Task<'a>) =
        task {
            let! a = ma
            return f a
        }

    let bind (f: 'a -> Task<'b>) (ma: Task<'a>) =
        task {
            let! a = ma
            return! f a
        }

[<RequireQualifiedAccess>]
module TaskResult =
    open System.Threading.Tasks

    let map (f: 'a -> 'b) (ma: Task<Result<'a, 'e>>) =
        task {
            let! a = ma
            return a |> Result.map f
        }

    let mapError (f: 'e -> 'e2) (ma: Task<Result<'a, 'e>>) =
        task {
            let! a = ma
            return a |> Result.mapError f
        }

    let bind (f: 'a -> Task<Result<'b, 'e>>) (ma: Task<Result<'a, 'e>>) =
        task {
            let! a = ma
            match a with
            | Ok a -> return! f a
            | Error e -> return Error e
        }

    let bindTask (f: 'a -> Task<'b>) (ma: Task<Result<'a, 'e>>) =
        task {
            let! a = ma
            match a with
            | Ok a -> return! f a |> Task.map Ok
            | Error e -> return Error e
        }

    let bindResult (f: 'a -> Result<'b, 'e>) (ma: Task<Result<'a, 'e>>) =
        task {
            let! a = ma
            return a |> Result.bind f
        }
