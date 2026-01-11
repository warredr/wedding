using System.Collections.Immutable;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;

namespace wedding_api.Tests;

internal sealed class TestFunctionContext : FunctionContext
{
    private IServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly CancellationToken _cancellationToken;

    public override string InvocationId { get; } = Guid.NewGuid().ToString("N");
    public override string FunctionId { get; } = "test";

    public override TraceContext TraceContext { get; } = new TestTraceContext();
    public override BindingContext BindingContext { get; } = new TestBindingContext();

    public override RetryContext? RetryContext { get; } = null;

    public override IServiceProvider InstanceServices
    {
        get => _services;
        set => _services = value;
    }

    public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

    public override IInvocationFeatures Features { get; } = new TestInvocationFeatures();

    public override FunctionDefinition FunctionDefinition { get; } = new TestFunctionDefinition();

    public override CancellationToken CancellationToken => _cancellationToken;

    public TestFunctionContext(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
    }

    private sealed class TestInvocationFeatures : IInvocationFeatures
    {
        private readonly Dictionary<Type, object> _features = new();

        public T Get<T>()
        {
            return _features.TryGetValue(typeof(T), out var value) ? (T)value : default!;
        }

        public void Set<T>(T instance)
        {
            if (instance is null)
            {
                _features.Remove(typeof(T));
                return;
            }

            _features[typeof(T)] = instance;
        }

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TestTraceContext : TraceContext
    {
        public override string TraceParent { get; } = string.Empty;
        public override string TraceState { get; } = string.Empty;
    }

    private sealed class TestBindingContext : BindingContext
    {
        public override IReadOnlyDictionary<string, object> BindingData { get; } = new Dictionary<string, object>();
    }

    private sealed class TestFunctionDefinition : FunctionDefinition
    {
        public override string PathToAssembly { get; } = string.Empty;
        public override string EntryPoint { get; } = string.Empty;
        public override string Id { get; } = "test";
        public override string Name { get; } = "test";
        public override IImmutableDictionary<string, BindingMetadata> InputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
        public override IImmutableDictionary<string, BindingMetadata> OutputBindings { get; } = ImmutableDictionary<string, BindingMetadata>.Empty;
        public override ImmutableArray<FunctionParameter> Parameters { get; } = ImmutableArray<FunctionParameter>.Empty;
    }
}

internal sealed class TestHttpRequestData : HttpRequestData
{
    private readonly MemoryStream _body;

    public TestHttpRequestData(FunctionContext functionContext, Uri url, string method = "GET", string? body = null)
        : base(functionContext)
    {
        Url = url;
        Method = method;
        Headers = new HttpHeadersCollection();
        Cookies = Array.Empty<IHttpCookie>();

        _body = new MemoryStream();
        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            _body.Write(bytes, 0, bytes.Length);
            _body.Position = 0;
        }
    }

    public override Stream Body => _body;
    public override HttpHeadersCollection Headers { get; }
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; }
    public override Uri Url { get; }
    public override IEnumerable<ClaimsIdentity> Identities => Array.Empty<ClaimsIdentity>();
    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(FunctionContext);
    }
}

internal sealed class TestHttpResponseData : HttpResponseData
{
    private readonly MemoryStream _body = new();

    public TestHttpResponseData(FunctionContext functionContext)
        : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Cookies = new TestHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body
    {
        get => _body;
        set => throw new NotSupportedException();
    }

    public override HttpCookies Cookies { get; }

    private sealed class TestHttpCookies : HttpCookies
    {
        private readonly List<IHttpCookie> _cookies = new();

        public override IHttpCookie CreateNew() => new HttpCookie(string.Empty, string.Empty);

        public override void Append(IHttpCookie cookie) => _cookies.Add(cookie);

        public override void Append(string name, string value) => _cookies.Add(new HttpCookie(name, value));
    }

    public string ReadBodyAsString()
    {
        _body.Position = 0;
        using var reader = new StreamReader(_body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
