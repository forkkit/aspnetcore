﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures.RazorComponents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.Net.Http.Headers;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures.Test
{
    public class HtmlHelperComponentExtensionsTests
    {
        private const string PrerenderedComponentPattern = "^<!--Blazor:(?<preamble>.*?)-->(?<content>.+?)<!--Blazor:(?<epilogue>.*?)-->$";
        private const string ComponentPattern = "^<!--Blazor:(.*?)-->$";

        private static readonly IDataProtectionProvider _dataprotectorProvider = new EphemeralDataProtectionProvider();

        [Fact]
        public async Task CanRender_ParameterlessComponent()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<TestComponent>(RenderMode.Static);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();

            // Assert
            Assert.Equal("<h1>Hello world!</h1>", content);
        }

        [Fact]
        public async Task CanRender_ParameterlessComponent_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<TestComponent>(RenderMode.Server);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ServerComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, marker.Sequence);
            Assert.Null(marker.PrerenderId);
            Assert.NotNull(marker.Descriptor);
            Assert.Equal("server", marker.Type);

            var unprotectedServerComponent = protector.Unprotect(marker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(TestComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(TestComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);
        }

        [Fact]
        public async Task CanPrerender_ParameterlessComponent_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<TestComponent>(RenderMode.ServerPrerendered);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ServerComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, preambleMarker.Sequence);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.NotNull(preambleMarker.Descriptor);
            Assert.Equal("server", preambleMarker.Type);

            var unprotectedServerComponent = protector.Unprotect(preambleMarker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotEqual(default, serverComponent);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(TestComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(TestComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<h1>Hello world!</h1>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ServerComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Sequence);
            Assert.Null(epilogueMarker.Descriptor);
            Assert.Null(epilogueMarker.Type);
        }

        [Fact]
        public async Task CanRender_ParameterlessComponent_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<TestComponent>(RenderMode.Client);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ClientComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Null(marker.PrerenderId);
            Assert.Equal("client", marker.Type);
            Assert.Equal(typeof(TestComponent).Assembly.GetName().Name, marker.Assembly);
            Assert.Equal(typeof(TestComponent).FullName, marker.TypeName);
        }

        [Fact]
        public async Task CanPrerender_ParameterlessComponent_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<TestComponent>(RenderMode.ClientPrerendered);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ClientComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.Equal("client", preambleMarker.Type);
            Assert.Equal(typeof(TestComponent).Assembly.GetName().Name, preambleMarker.Assembly);
            Assert.Equal(typeof(TestComponent).FullName, preambleMarker.TypeName);

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<h1>Hello world!</h1>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ClientComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Assembly);
            Assert.Null(epilogueMarker.TypeName);
            Assert.Null(epilogueMarker.Type);
            Assert.Null(epilogueMarker.ParameterDefinitions);
            Assert.Null(epilogueMarker.ParameterValues);
        }

        [Fact]
        public async Task CanRender_ComponentWithParameters_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.Client,
                new
                {
                    Name = "Daniel"
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ClientComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Null(marker.PrerenderId);
            Assert.Equal("client", marker.Type);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, marker.Assembly);
            Assert.Equal(typeof(GreetingComponent).FullName, marker.TypeName);

            var parameterDefinition = Assert.Single(marker.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Equal("System.String", parameterDefinition.TypeName);
            Assert.Equal("System.Private.CoreLib", parameterDefinition.Assembly);

            var value = Assert.Single(marker.ParameterValues);
            var rawValue = Assert.IsType<JsonElement>(value);
            Assert.Equal("Daniel", rawValue.GetString());
        }

        [Fact]
        public async Task CanRender_ComponentWithNullParameters_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.Client,
                new
                {
                    Name = (string)null
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ClientComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Null(marker.PrerenderId);
            Assert.Equal("client", marker.Type);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, marker.Assembly);
            Assert.Equal(typeof(GreetingComponent).FullName, marker.TypeName);

            var parameterDefinition = Assert.Single(marker.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Null(parameterDefinition.TypeName);
            Assert.Null(parameterDefinition.Assembly);

            var value = Assert.Single(marker.ParameterValues);
            Assert.Null(value);
        }

        [Fact]
        public async Task CanPrerender_ComponentWithParameters_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.ClientPrerendered,
                new
                {
                    Name = "Daniel"
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ClientComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.Equal("client", preambleMarker.Type);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, preambleMarker.Assembly);
            Assert.Equal(typeof(GreetingComponent).FullName, preambleMarker.TypeName);

            var parameterDefinition = Assert.Single(preambleMarker.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Equal("System.String", parameterDefinition.TypeName);
            Assert.Equal("System.Private.CoreLib", parameterDefinition.Assembly);

            var value = Assert.Single(preambleMarker.ParameterValues);
            var rawValue = Assert.IsType<JsonElement>(value);
            Assert.Equal("Daniel", rawValue.GetString());

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<p>Hello Daniel!</p>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ClientComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Assembly);
            Assert.Null(epilogueMarker.TypeName);
            Assert.Null(epilogueMarker.Type);
            Assert.Null(epilogueMarker.ParameterDefinitions);
            Assert.Null(epilogueMarker.ParameterValues);
        }

        [Fact]
        public async Task CanPrerender_ComponentWithNullParameters_ClientMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.ClientPrerendered,
                new
                {
                    Name = (string)null
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ClientComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.Equal("client", preambleMarker.Type);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, preambleMarker.Assembly);
            Assert.Equal(typeof(GreetingComponent).FullName, preambleMarker.TypeName);

            var parameterDefinition = Assert.Single(preambleMarker.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Null(parameterDefinition.TypeName);
            Assert.Null(parameterDefinition.Assembly);

            var value = Assert.Single(preambleMarker.ParameterValues);
            Assert.Null(value);

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<p>Hello (null)!</p>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ClientComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Assembly);
            Assert.Null(epilogueMarker.TypeName);
            Assert.Null(epilogueMarker.Type);
            Assert.Null(epilogueMarker.ParameterDefinitions);
            Assert.Null(epilogueMarker.ParameterValues);
        }

        [Fact]
        public async Task CanRenderMultipleServerComponents()
        {
            // Arrange
            var helper = CreateHelper();
            var firstWriter = new StringWriter();
            var secondWriter = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var firstResult = await helper.RenderComponentAsync<TestComponent>(RenderMode.ServerPrerendered);
            firstResult.WriteTo(firstWriter, HtmlEncoder.Default);
            var firstComponent = firstWriter.ToString();
            var firstMatch = Regex.Match(firstComponent, PrerenderedComponentPattern, RegexOptions.Multiline);

            var secondResult = await helper.RenderComponentAsync<TestComponent>(RenderMode.Server);
            secondResult.WriteTo(secondWriter, HtmlEncoder.Default);
            var secondComponent = secondWriter.ToString();
            var secondMatch = Regex.Match(secondComponent, ComponentPattern);

            // Assert
            Assert.True(firstMatch.Success);
            var preamble = firstMatch.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ServerComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, preambleMarker.Sequence);
            Assert.NotNull(preambleMarker.Descriptor);

            var unprotectedFirstServerComponent = protector.Unprotect(preambleMarker.Descriptor);
            var firstServerComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedFirstServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, firstServerComponent.Sequence);
            Assert.NotEqual(Guid.Empty, firstServerComponent.InvocationId);

            Assert.True(secondMatch.Success);
            var marker = secondMatch.Groups[1].Value;
            var markerMarker = JsonSerializer.Deserialize<ServerComponentMarker>(marker, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(1, markerMarker.Sequence);
            Assert.NotNull(markerMarker.Descriptor);

            var unprotectedSecondServerComponent = protector.Unprotect(markerMarker.Descriptor);
            var secondServerComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedSecondServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(1, secondServerComponent.Sequence);

            Assert.Equal(firstServerComponent.InvocationId, secondServerComponent.InvocationId);
        }

        [Fact]
        public async Task CanRender_ComponentWithParametersObject()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.Static,
                new
                {
                    Name = "Steve"
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();

            // Assert
            Assert.Equal("<p>Hello Steve!</p>", content);
        }

        [Fact]
        public async Task CanRender_ComponentWithParameters_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.Server,
                new
                {
                    Name = "Daniel"
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ServerComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, marker.Sequence);
            Assert.Null(marker.PrerenderId);
            Assert.NotNull(marker.Descriptor);
            Assert.Equal("server", marker.Type);

            var unprotectedServerComponent = protector.Unprotect(marker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(GreetingComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);

            var parameterDefinition = Assert.Single(serverComponent.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Equal("System.String", parameterDefinition.TypeName);
            Assert.Equal("System.Private.CoreLib", parameterDefinition.Assembly);

            var value = Assert.Single(serverComponent.ParameterValues);
            var rawValue = Assert.IsType<JsonElement>(value);
            Assert.Equal("Daniel", rawValue.GetString());
        }

        [Fact]
        public async Task CanRender_ComponentWithNullParameters_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.Server,
                new
                {
                    Name = (string)null
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, ComponentPattern);

            // Assert
            Assert.True(match.Success);
            var marker = JsonSerializer.Deserialize<ServerComponentMarker>(match.Groups[1].Value, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, marker.Sequence);
            Assert.Null(marker.PrerenderId);
            Assert.NotNull(marker.Descriptor);
            Assert.Equal("server", marker.Type);

            var unprotectedServerComponent = protector.Unprotect(marker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(GreetingComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);

            Assert.NotNull(serverComponent.ParameterDefinitions);
            var parameterDefinition = Assert.Single(serverComponent.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Null(parameterDefinition.TypeName);
            Assert.Null(parameterDefinition.Assembly);

            var value = Assert.Single(serverComponent.ParameterValues);;
            Assert.Null(value);
        }

        [Fact]
        public async Task CanPrerender_ComponentWithParameters_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.ServerPrerendered,
                new
                {
                    Name = "Daniel"
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ServerComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, preambleMarker.Sequence);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.NotNull(preambleMarker.Descriptor);
            Assert.Equal("server", preambleMarker.Type);

            var unprotectedServerComponent = protector.Unprotect(preambleMarker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotEqual(default, serverComponent);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(GreetingComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);

            var parameterDefinition = Assert.Single(serverComponent.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Equal("System.String", parameterDefinition.TypeName);
            Assert.Equal("System.Private.CoreLib", parameterDefinition.Assembly);

            var value = Assert.Single(serverComponent.ParameterValues);
            var rawValue = Assert.IsType<JsonElement>(value);
            Assert.Equal("Daniel", rawValue.GetString());

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<p>Hello Daniel!</p>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ServerComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Sequence);
            Assert.Null(epilogueMarker.Descriptor);
            Assert.Null(epilogueMarker.Type);
        }

        [Fact]
        public async Task CanPrerender_ComponentWithNullParameters_ServerMode()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var protector = _dataprotectorProvider.CreateProtector(ServerComponentSerializationSettings.DataProtectionProviderPurpose)
                .ToTimeLimitedDataProtector();

            // Act
            var result = await helper.RenderComponentAsync<GreetingComponent>(
                RenderMode.ServerPrerendered,
                new
                {
                    Name = (string)null
                });
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();
            var match = Regex.Match(content, PrerenderedComponentPattern, RegexOptions.Multiline);

            // Assert
            Assert.True(match.Success);
            var preamble = match.Groups["preamble"].Value;
            var preambleMarker = JsonSerializer.Deserialize<ServerComponentMarker>(preamble, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(0, preambleMarker.Sequence);
            Assert.NotNull(preambleMarker.PrerenderId);
            Assert.NotNull(preambleMarker.Descriptor);
            Assert.Equal("server", preambleMarker.Type);

            var unprotectedServerComponent = protector.Unprotect(preambleMarker.Descriptor);
            var serverComponent = JsonSerializer.Deserialize<ServerComponent>(unprotectedServerComponent, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.NotEqual(default, serverComponent);
            Assert.Equal(0, serverComponent.Sequence);
            Assert.Equal(typeof(GreetingComponent).Assembly.GetName().Name, serverComponent.AssemblyName);
            Assert.Equal(typeof(GreetingComponent).FullName, serverComponent.TypeName);
            Assert.NotEqual(Guid.Empty, serverComponent.InvocationId);

            Assert.NotNull(serverComponent.ParameterDefinitions);
            var parameterDefinition = Assert.Single(serverComponent.ParameterDefinitions);
            Assert.Equal("Name", parameterDefinition.Name);
            Assert.Null(parameterDefinition.TypeName);
            Assert.Null(parameterDefinition.Assembly);

            var value = Assert.Single(serverComponent.ParameterValues);
            Assert.Null(value);

            var prerenderedContent = match.Groups["content"].Value;
            Assert.Equal("<p>Hello (null)!</p>", prerenderedContent);

            var epilogue = match.Groups["epilogue"].Value;
            var epilogueMarker = JsonSerializer.Deserialize<ServerComponentMarker>(epilogue, ServerComponentSerializationSettings.JsonSerializationOptions);
            Assert.Equal(preambleMarker.PrerenderId, epilogueMarker.PrerenderId);
            Assert.Null(epilogueMarker.Sequence);
            Assert.Null(epilogueMarker.Descriptor);
            Assert.Null(epilogueMarker.Type);
        }

        [Fact]
        public async Task ComponentWithInvalidRenderMode_Throws()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act & Assert
            var result = await Assert.ThrowsAsync<ArgumentException>(() => helper.RenderComponentAsync<GreetingComponent>(
                default,
                new
                {
                    Name = "Steve"
                }));
            Assert.Equal("renderMode", result.ParamName);
        }

        [Fact]
        public async Task RenderComponent_DoesNotInvokeOnAfterRenderInComponent()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();

            // Act
            var state = new OnAfterRenderState();
            var result = await helper.RenderComponentAsync<OnAfterRenderComponent>(
                RenderMode.Static,
                new
                {
                    State = state
                });

            result.WriteTo(writer, HtmlEncoder.Default);

            // Assert
            Assert.Equal("<p>Hello</p>", writer.ToString());
            Assert.False(state.OnAfterRenderRan);
        }

        [Fact]
        public async Task CanCatch_ComponentWithSynchronousException()
        {
            // Arrange
            var helper = CreateHelper();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.RenderComponentAsync<ExceptionComponent>(
                RenderMode.Static,
                new
                {
                    IsAsync = false
                }));

            // Assert
            Assert.Equal("Threw an exception synchronously", exception.Message);
        }

        [Fact]
        public async Task CanCatch_ComponentWithAsynchronousException()
        {
            // Arrange
            var helper = CreateHelper();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.RenderComponentAsync<ExceptionComponent>(
                RenderMode.Static,
                new
                {
                    IsAsync = true
                }));

            // Assert
            Assert.Equal("Threw an exception asynchronously", exception.Message);
        }

        [Fact]
        public async Task Rendering_ComponentWithJsInteropThrows()
        {
            // Arrange
            var helper = CreateHelper();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.RenderComponentAsync<ExceptionComponent>(
                RenderMode.Static,
                new
                {
                    JsInterop = true
                }
            ));

            // Assert
            Assert.Equal("JavaScript interop calls cannot be issued during server-side prerendering, " +
                    "because the page has not yet loaded in the browser. Prerendered components must wrap any JavaScript " +
                    "interop calls in conditional logic to ensure those interop calls are not attempted during prerendering.",
                exception.Message);
        }

        [Fact]
        public async Task UriHelperRedirect_ThrowsInvalidOperationException_WhenResponseHasAlreadyStarted()
        {
            // Arrange
            var ctx = new DefaultHttpContext();
            ctx.Request.Scheme = "http";
            ctx.Request.Host = new HostString("localhost");
            ctx.Request.PathBase = "/base";
            ctx.Request.Path = "/path";
            ctx.Request.QueryString = new QueryString("?query=value");
            var responseMock = new Mock<IHttpResponseFeature>();
            responseMock.Setup(r => r.HasStarted).Returns(true);
            ctx.Features.Set(responseMock.Object);
            var helper = CreateHelper(ctx);
            var writer = new StringWriter();

            // Act
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => helper.RenderComponentAsync<RedirectComponent>(
                RenderMode.Static,
                new
                {
                    RedirectUri = "http://localhost/redirect"
                }));

            Assert.Equal("A navigation command was attempted during prerendering after the server already started sending the response. " +
                            "Navigation commands can not be issued during server-side prerendering after the response from the server has started. Applications must buffer the" +
                            "reponse and avoid using features like FlushAsync() before all components on the page have been rendered to prevent failed navigation commands.",
                exception.Message);
        }

        [Fact]
        public async Task HtmlHelper_Redirects_WhenComponentNavigates()
        {
            // Arrange
            var ctx = new DefaultHttpContext();
            ctx.Request.Scheme = "http";
            ctx.Request.Host = new HostString("localhost");
            ctx.Request.PathBase = "/base";
            ctx.Request.Path = "/path";
            ctx.Request.QueryString = new QueryString("?query=value");
            var helper = CreateHelper(ctx);

            // Act
            await helper.RenderComponentAsync<RedirectComponent>(
                RenderMode.Static,
                new
                {
                    RedirectUri = "http://localhost/redirect"
                });

            // Assert
            Assert.Equal(302, ctx.Response.StatusCode);
            Assert.Equal("http://localhost/redirect", ctx.Response.Headers[HeaderNames.Location]);
        }

        [Fact]
        public async Task CanRender_AsyncComponent()
        {
            // Arrange
            var helper = CreateHelper();
            var writer = new StringWriter();
            var expectedContent = @"<table>
<thead>
<tr>
<th>Date</th>
<th>Summary</th>
<th>F</th>
<th>C</th>
</tr>
</thead>
<tbody>
<tr>
<td>06/05/2018</td>
<td>Freezing</td>
<td>33</td>
<td>33</td>
</tr>
<tr>
<td>07/05/2018</td>
<td>Bracing</td>
<td>57</td>
<td>57</td>
</tr>
<tr>
<td>08/05/2018</td>
<td>Freezing</td>
<td>9</td>
<td>9</td>
</tr>
<tr>
<td>09/05/2018</td>
<td>Balmy</td>
<td>4</td>
<td>4</td>
</tr>
<tr>
<td>10/05/2018</td>
<td>Chilly</td>
<td>29</td>
<td>29</td>
</tr>
</tbody>
</table>";

            // Act
            var result = await helper.RenderComponentAsync<AsyncComponent>(RenderMode.Static);
            result.WriteTo(writer, HtmlEncoder.Default);
            var content = writer.ToString();

            // Assert
            Assert.Equal(expectedContent.Replace("\r\n", "\n"), content);
        }

        private static IHtmlHelper CreateHelper(HttpContext ctx = null, Action<IServiceCollection> configureServices = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(HtmlEncoder.Default);
            services.AddSingleton<ServerComponentSerializer>();
            services.AddSingleton(_dataprotectorProvider);
            services.AddSingleton<IJSRuntime, UnsupportedJavaScriptRuntime>();
            services.AddSingleton<NavigationManager, HttpNavigationManager>();
            services.AddSingleton<StaticComponentRenderer>();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

            configureServices?.Invoke(services);

            var helper = new Mock<IHtmlHelper>();
            var context = ctx ?? new DefaultHttpContext();
            context.RequestServices = services.BuildServiceProvider();
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost");
            context.Request.PathBase = "/base";
            context.Request.Path = "/path";
            context.Request.QueryString = QueryString.FromUriComponent("?query=value");

            helper.Setup(h => h.ViewContext)
                .Returns(new ViewContext()
                {
                    HttpContext = context
                });
            return helper.Object;
        }

        private class TestComponent : IComponent
        {
            private RenderHandle _renderHandle;

            public void Attach(RenderHandle renderHandle)
            {
                _renderHandle = renderHandle;
            }

            public Task SetParametersAsync(ParameterView parameters)
            {
                _renderHandle.Render(builder =>
                {
                    var s = 0;
                    builder.OpenElement(s++, "h1");
                    builder.AddContent(s++, "Hello world!");
                    builder.CloseElement();
                });
                return Task.CompletedTask;
            }
        }

        private class RedirectComponent : ComponentBase
        {
            [Inject] NavigationManager NavigationManager { get; set; }

            [Parameter] public string RedirectUri { get; set; }

            [Parameter] public bool Force { get; set; }

            protected override void OnInitialized()
            {
                NavigationManager.NavigateTo(RedirectUri, Force);
            }
        }

        private class ExceptionComponent : ComponentBase
        {
            [Parameter] public bool IsAsync { get; set; }

            [Parameter] public bool JsInterop { get; set; }

            [Inject] IJSRuntime JsRuntime { get; set; }

            protected override async Task OnParametersSetAsync()
            {
                if (JsInterop)
                {
                    await JsRuntime.InvokeAsync<int>("window.alert", "Interop!");
                }

                if (!IsAsync)
                {
                    throw new InvalidOperationException("Threw an exception synchronously");
                }
                else
                {
                    await Task.Yield();
                    throw new InvalidOperationException("Threw an exception asynchronously");
                }
            }
        }

        private class OnAfterRenderComponent : ComponentBase
        {
            [Parameter] public OnAfterRenderState State { get; set; }

            protected override void OnAfterRender(bool firstRender)
            {
                State.OnAfterRenderRan = true;
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.AddMarkupContent(0, "<p>Hello</p>");
            }
        }

        private class OnAfterRenderState
        {
            public bool OnAfterRenderRan { get; set; }
        }

        private class GreetingComponent : ComponentBase
        {
            [Parameter] public string Name { get; set; }

            protected override void OnParametersSet()
            {
                base.OnParametersSet();
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                var s = 0;
                base.BuildRenderTree(builder);
                builder.OpenElement(s++, "p");
                builder.AddContent(s++, $"Hello {Name ?? ("(null)")}!");
                builder.CloseElement();
            }
        }

        private class AsyncComponent : ComponentBase
        {
            private static WeatherRow[] _weatherData = new[]
            {
                new WeatherRow
                {
                    DateFormatted = "06/05/2018",
                    TemperatureC = 1,
                    Summary = "Freezing",
                    TemperatureF = 33
                },
                new WeatherRow
                {
                    DateFormatted = "07/05/2018",
                    TemperatureC = 14,
                    Summary = "Bracing",
                    TemperatureF = 57
                },
                new WeatherRow
                {
                    DateFormatted = "08/05/2018",
                    TemperatureC = -13,
                    Summary = "Freezing",
                    TemperatureF = 9
                },
                new WeatherRow
                {
                    DateFormatted = "09/05/2018",
                    TemperatureC = -16,
                    Summary = "Balmy",
                    TemperatureF = 4
                },
                new WeatherRow
                {
                    DateFormatted = "10/05/2018",
                    TemperatureC = 2,
                    Summary = "Chilly",
                    TemperatureF = 29
                }
            };

            public class WeatherRow
            {
                public string DateFormatted { get; set; }
                public int TemperatureC { get; set; }
                public string Summary { get; set; }
                public int TemperatureF { get; set; }
            }

            public WeatherRow[] RowsToDisplay { get; set; }

            protected override async Task OnParametersSetAsync()
            {
                // Simulate an async workflow.
                await Task.Yield();
                RowsToDisplay = _weatherData;
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                base.BuildRenderTree(builder);
                var s = 0;
                builder.OpenElement(s++, "table");
                builder.AddMarkupContent(s++, "\n");
                builder.OpenElement(s++, "thead");
                builder.AddMarkupContent(s++, "\n");
                builder.OpenElement(s++, "tr");
                builder.AddMarkupContent(s++, "\n");

                builder.OpenElement(s++, "th");
                builder.AddContent(s++, "Date");
                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");

                builder.OpenElement(s++, "th");
                builder.AddContent(s++, "Summary");
                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");

                builder.OpenElement(s++, "th");
                builder.AddContent(s++, "F");
                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");

                builder.OpenElement(s++, "th");
                builder.AddContent(s++, "C");
                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");

                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");
                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");
                builder.OpenElement(s++, "tbody");
                builder.AddMarkupContent(s++, "\n");
                if (RowsToDisplay != null)
                {
                    var s2 = s;
                    foreach (var element in RowsToDisplay)
                    {
                        s = s2;
                        builder.OpenElement(s++, "tr");
                        builder.AddMarkupContent(s++, "\n");

                        builder.OpenElement(s++, "td");
                        builder.AddContent(s++, element.DateFormatted);
                        builder.CloseElement();
                        builder.AddMarkupContent(s++, "\n");

                        builder.OpenElement(s++, "td");
                        builder.AddContent(s++, element.Summary);
                        builder.CloseElement();
                        builder.AddMarkupContent(s++, "\n");

                        builder.OpenElement(s++, "td");
                        builder.AddContent(s++, element.TemperatureF);
                        builder.CloseElement();
                        builder.AddMarkupContent(s++, "\n");

                        builder.OpenElement(s++, "td");
                        builder.AddContent(s++, element.TemperatureF);
                        builder.CloseElement();
                        builder.AddMarkupContent(s++, "\n");

                        builder.CloseElement();
                        builder.AddMarkupContent(s++, "\n");
                    }
                }

                builder.CloseElement();
                builder.AddMarkupContent(s++, "\n");

                builder.CloseElement();
            }
        }
    }
}
