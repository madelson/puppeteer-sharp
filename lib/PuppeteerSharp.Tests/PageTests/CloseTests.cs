using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PageTests
{
    [Collection(TestConstants.TestFixtureCollectionName)]
    public class CloseTests : PuppeteerPageBaseTest
    {
        public CloseTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public async Task ShouldRejectAllPromisesWhenPageIsClosed()
        {
            var exceptionTask = Assert.ThrowsAsync<TargetClosedException>(() => Page.EvaluateFunctionAsync("() => new Promise(r => {})"));

            await Task.WhenAll(
                exceptionTask,
                Page.CloseAsync()
            );

            var exception = await exceptionTask;
            Assert.Contains("Protocol error", exception.Message);
            Assert.Equal("Target.detachedFromTarget", exception.CloseReason);
        }

        [Fact]
        public async Task ShouldNotBeVisibleInBrowserPages()
        {
            Assert.Contains(Page, await Browser.PagesAsync());
            await Page.CloseAsync();
            Assert.DoesNotContain(Page, await Browser.PagesAsync());
        }

        [Fact]
        public async Task ShouldRunBeforeunloadIfAskedFor()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/beforeunload.html");

            var dialogTask = new TaskCompletionSource<bool>();
            Page.Dialog += async (sender, e) =>
            {
                Assert.Equal(DialogType.BeforeUnload, e.Dialog.DialogType);
                Assert.Equal(string.Empty, e.Dialog.Message);
                Assert.Equal(string.Empty, e.Dialog.DefaultValue);

                await e.Dialog.Accept();
                dialogTask.TrySetResult(true);
            };

            var closeTask = new TaskCompletionSource<bool>();
            Page.Close += (sender, e) => closeTask.TrySetResult(true);

            // We have to interact with a page so that 'beforeunload' handlers
            // fire.
            await Page.ClickAsync("body");
            await Page.CloseAsync(new PageCloseOptions { RunBeforeUnload = true });

            await Task.WhenAll(
                dialogTask.Task,
                closeTask.Task
            );
        }

        [Fact]
        public async Task ShouldNotRunBeforeunloadByDefault()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/beforeunload.html");
            await Page.ClickAsync("body");
            await Page.CloseAsync();
        }

        [Fact]
        public async Task ShouldSetThePageCloseState()
        {
            Assert.False(Page.IsClosed);
            await Page.CloseAsync();
            Assert.True(Page.IsClosed);
        }

        [Fact]
        public async Task ShouldTerminateNetworkWaiters()
        {
            var newPage = await Context.NewPageAsync();
            var requestTask = newPage.WaitForRequestAsync(TestConstants.EmptyPage);
            var responseTask = newPage.WaitForResponseAsync(TestConstants.EmptyPage);

            await newPage.CloseAsync();

            var exception = await Assert.ThrowsAsync<TargetClosedException>(() => requestTask);
            Assert.Contains("Target closed", exception.Message);
            Assert.DoesNotContain("Timeout", exception.Message);

            exception = await Assert.ThrowsAsync<TargetClosedException>(() => responseTask);
            Assert.Contains("Target closed", exception.Message);
            Assert.DoesNotContain("Timeout", exception.Message);
        }

        [Fact(Timeout = 10000)]
        public async Task ShouldCloseWhenConnectionBreaksPrematurely()
        {
            Browser.Connection.Dispose();
            await Page.CloseAsync();
        }

        /// <summary>
        /// Prior to addressing https://github.com/kblok/puppeteer-sharp/issues/1354, <see cref="Page.Dispose"/>
        /// would "fire and forget", which meant that the page might not actually be closed upon completion.
        /// This test demonstrates that disposing the browser immediately closes it.
        /// </summary>
        [Fact]
        public async Task DisposeShouldSynchronouslyClosePage()
        {
            var page = await Context.NewPageAsync();
            page.Dispose();
            Assert.True(page.IsClosed);
        }

        /// <summary>
        /// Prior to addressing https://github.com/kblok/puppeteer-sharp/issues/1354, waiting on
        /// <see cref="Page.CloseAsync"/> could deadlock because the messages coming from 
        /// chromium were processed synchronously in a way that blocked the event loop. This test
        /// demonstrates that it is now possible to wait synchronously on <see cref="Page.CloseAsync"/>
        /// without hanging.
        /// </summary>
        [Fact]
        public async Task ShouldBeAbleToSynchronouslyWaitOnCloseAsync()
        {
            using (var page = await Context.NewPageAsync())
            {
                // 10s should be MUCH longer than we need for close to finish, so we should only
                // fail if the deadlock is actually encountered
                Assert.True(page.CloseAsync().Wait(TimeSpan.FromSeconds(10)));
            }
        }
    }
}