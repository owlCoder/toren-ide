using NUnit.Framework;
using Toren.App.Http.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Http.Contracts;
using Toren.DotNet.Http.Models;
using Toren.DotNet.Http.Parsers;
using Toren.DotNet.Http.Services;

namespace Toren.UnitTests.Http;

[TestFixture]
public sealed class HttpClientViewModelTests
{
    [Test]
    public async Task ActiveHttpDocumentCanSendSelectedEnvironmentRequestAndRecordResponse()
    {
        var runner = new RecordingRunner();
        var viewModel = CreateViewModel(runner);
        viewModel.SetEnvironments(
        [
            new HttpEnvironment(
                "Development",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrl"] = "https://example.test",
                }),
        ]);
        viewModel.SetDocument(
            Path.Combine(Path.GetTempPath(), "requests.http"),
            "# @name listWidgets\nGET {{baseUrl}}/api/widgets");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Requests, Has.Count.EqualTo(1));
            Assert.That(viewModel.Environments, Has.Count.EqualTo(1));
            Assert.That(viewModel.SelectedEnvironment?.Name, Is.EqualTo("Development"));
            Assert.That(viewModel.SelectedRequest?.Name, Is.EqualTo("listWidgets"));
            Assert.That(viewModel.CanSend, Is.True);
        });

        await viewModel.SendSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(runner.LastRequest?.RequestTarget, Is.EqualTo("https://example.test/api/widgets"));
            Assert.That(viewModel.ResponseStatus, Does.StartWith("200 OK"));
            Assert.That(viewModel.ResponseBody, Does.Contain(System.Environment.NewLine));
            Assert.That(viewModel.ResponseHeaders, Does.Contain("Content-Type: application/json"));
            Assert.That(viewModel.History, Has.Count.EqualTo(1));
            Assert.That(viewModel.History[0].RequestTarget, Is.EqualTo("https://example.test/api/widgets"));
            Assert.That(viewModel.IsBusy, Is.False);
        });
    }

    [Test]
    public void EnvironmentSelectionChangesResolvedVariables()
    {
        var viewModel = CreateViewModel(new RecordingRunner());
        viewModel.SetEnvironments(
        [
            new HttpEnvironment(
                "Development",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrl"] = "https://dev.example.test",
                }),
            new HttpEnvironment(
                "Production",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrl"] = "https://prod.example.test",
                }),
        ]);
        viewModel.SetDocument(
            Path.Combine(Path.GetTempPath(), "requests.http"),
            "GET {{baseUrl}}/health");

        Assert.That(viewModel.SelectedEnvironment?.Name, Is.EqualTo("Development"));
        viewModel.SelectedEnvironmentIndex = 1;

        Assert.That(viewModel.SelectedEnvironment?.Name, Is.EqualTo("Production"));
    }

    [Test]
    public void SetEnvironmentsPreservesSelectionByNameWhenReloading()
    {
        var viewModel = CreateViewModel(new RecordingRunner());
        viewModel.SetEnvironments(
        [
            CreateEnvironment("Development", "https://dev.example.test"),
            CreateEnvironment("Production", "https://prod.example.test"),
        ]);
        viewModel.SelectedEnvironmentIndex = 1;

        viewModel.SetEnvironments(
        [
            CreateEnvironment("Production", "https://prod2.example.test"),
            CreateEnvironment("Development", "https://dev2.example.test"),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedEnvironment?.Name, Is.EqualTo("Production"));
            Assert.That(viewModel.SelectedEnvironment?.Variables["baseUrl"], Is.EqualTo("https://prod2.example.test"));
        });
    }

    [Test]
    public void NonHttpDocumentClearsRequestSelection()
    {
        var viewModel = CreateViewModel(new RecordingRunner());
        viewModel.SetDocument(
            Path.Combine(Path.GetTempPath(), "requests.http"),
            "GET https://example.test/api/widgets");
        Assert.That(viewModel.CanSend, Is.True);

        viewModel.SetDocument(
            Path.Combine(Path.GetTempPath(), "Program.cs"),
            "class Program { }");

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Requests, Is.Empty);
            Assert.That(viewModel.SelectedRequest, Is.Null);
            Assert.That(viewModel.CanSend, Is.False);
            Assert.That(viewModel.StatusText, Does.Contain(".http"));
        });
    }

    private static HttpEnvironment CreateEnvironment(string name, string baseUrl) =>
        new(
            name,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["baseUrl"] = baseUrl,
            });

    private static HttpClientViewModel CreateViewModel(IHttpRequestRunner runner) =>
        new(
            new HttpRequestDocumentParser(),
            new HttpRequestVariableResolver(),
            runner,
            new HttpResponseFormatter(),
            new HttpRequestHistory());

    private sealed class RecordingRunner : IHttpRequestRunner
    {
        public HttpRequestDefinition? LastRequest { get; private set; }

        public Task<Result<HttpResponseSnapshot>> ExecuteAsync(
            HttpRequestDefinition request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = new List<string> { "application/json" },
            };
            return Task.FromResult(Result.Success(new HttpResponseSnapshot(
                200,
                "OK",
                headers,
                "{\"count\":2}",
                TimeSpan.FromMilliseconds(12))));
        }
    }
}
