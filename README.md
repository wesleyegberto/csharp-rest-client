# C# Rest Client

Simple REST client builder around `HttpClient` to C#.


### Usage

```
 await HttpClientBuilder.Create("my.api.com")
    .Path("entities").Path(myEntityId)
    .Query("v", 1)
    .QueryDate("from", DateTime.Now)
    .Header("x-track", "token-123")
    .AsyncGet()
    .GetEntity<MyEntity>();
```