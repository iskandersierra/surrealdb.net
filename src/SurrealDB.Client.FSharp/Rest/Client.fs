namespace SurrealDB.Client.FSharp.Rest

open System.Text.Json

type ISurrealRestClient =
    abstract Json : ISurrealRestJsonClient
    abstract KeyValue : ISurrealRestKeyValueClient

type SurrealRestClient(config, httpClient, ?jsonOptions) =
    do Endpoints.applyConfig config httpClient

    let jsonOptions =
        jsonOptions
        |> Option.defaultValue (JsonSerializerOptions(JsonSerializerDefaults.General))

    let jsonClient = SurrealRestJsonClient(httpClient, jsonOptions)
    let keyValueClient = SurrealRestKeyValueClient(jsonClient, jsonOptions)

    interface ISurrealRestClient with
        member this.Json = jsonClient
        member this.KeyValue = keyValueClient

    interface System.IDisposable with
        member this.Dispose() = httpClient.Dispose()
