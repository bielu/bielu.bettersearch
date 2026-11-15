---
"bielu-bettersearch": minor
---

New package `Bielu.BetterSearch.OpenSearch.Aws` (#152): `AddOpenSearchAwsSigV4()` signs every OpenSearch request with AWS SigV4, through `OpenSearch.Net.Auth.AwsSigV4`, for Amazon OpenSearch Service with IAM authentication (`Service: es`, the default) and Amazon OpenSearch Serverless (`Service: aoss`). `Service` and `Region` are read from `Bielu:BetterSearch:OpenSearch:AwsSigV4` and validated at startup; an unset `Region` and unset `Credentials` come from the AWS SDK's defaults. The base OpenSearch package takes no AWS dependency, and gains `UseOpenSearchConnection(Func<IServiceProvider, IConnection>)`, which sends every OpenSearch client's requests through the connection the callback returns, once per cached client; without it the client keeps its default `HttpConnection`. The signing is tested against a local endpoint, not a real Amazon OpenSearch domain.
