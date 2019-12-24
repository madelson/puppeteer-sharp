using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.BrowserTests.Events
{
    [Collection(TestConstants.TestFixtureCollectionName)]
    public class BrowserCloseTests : PuppeteerBrowserBaseTest
    {
        public BrowserCloseTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldTerminateNetworkWaiters()
        {
            using (var browser = await Puppeteer.LaunchAsync(TestConstants.DefaultBrowserOptions()))
            using (var remote = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint = browser.WebSocketEndpoint }))
            {
                var newPage = await remote.NewPageAsync();
                var requestTask = newPage.WaitForRequestAsync(TestConstants.EmptyPage);
                var responseTask = newPage.WaitForResponseAsync(TestConstants.EmptyPage);

                await browser.CloseAsync();

                var exception = await Assert.ThrowsAsync<TargetClosedException>(() => requestTask);
                Assert.Contains("Target closed", exception.Message);
                Assert.DoesNotContain("Timeout", exception.Message);

                exception = await Assert.ThrowsAsync<TargetClosedException>(() => responseTask);
                Assert.Contains("Target closed", exception.Message);
                Assert.DoesNotContain("Timeout", exception.Message);
            }
        }

        /// <summary>
        /// Prior to addressing https://github.com/kblok/puppeteer-sharp/issues/1354, <see cref="Browser.Dispose"/>
        /// would "fire and forget", which meant that the browser might not actually be closed upon completion.
        /// This test demonstrates that disposing the browser immediately closes it.
        /// </summary>
        [Fact]
        public async Task DisposeShouldSynchronouslyCloseBrowser()
        {
            var browser = await Puppeteer.LaunchAsync(TestConstants.DefaultBrowserOptions());
            browser.Dispose();
            Assert.True(browser.IsClosed);
        }

        /// <summary>
        /// Prior to addressing https://github.com/kblok/puppeteer-sharp/issues/1354, waiting on
        /// <see cref="Browser.CloseAsync"/> could deadlock because the messages coming from 
        /// chromium were processed synchronously in a way that blocked the event loop. This test
        /// demonstrates that it is now possible to wait synchronously on <see cref="Browser.CloseAsync"/>
        /// without hanging.
        /// </summary>
        [Fact]
        public async Task ShouldBeAbleToSynchronouslyWaitOnCloseAsync()
        {
            using (var browser = await Puppeteer.LaunchAsync(TestConstants.DefaultBrowserOptions()))
            {
                // 10s should be MUCH longer than we need for close to finish, so we should only
                // fail if the deadlock is actually encountered
                Assert.True(browser.CloseAsync().Wait(TimeSpan.FromSeconds(10)));
            }
        }
    }
}