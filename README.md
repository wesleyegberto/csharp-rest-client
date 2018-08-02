# csharp-rest-client

Simple REST client to C#


### Usage

```
 await HttpClientBuilder.Create("my.api.com")
    .Path("entities").Path(myEntityId)
    .Query("v", 1)
    .Header("x-track", "token-123")
    .AsyncGet()
    .GetEntity<MyEntity>();
```