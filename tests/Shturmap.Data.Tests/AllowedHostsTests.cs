using System.Net;
using Shturmap.Data.Http;

namespace Shturmap.Data.Tests;

// The code names only tarkov.dev's hosts and GitHub (SafetyTests), but maps.json, a file in someone else's
// repository, gives each map's artwork and tile addresses. The app's client asks no other host, whatever the data
// says (review of 2026-10-04).
public class AllowedHostsTests
{
    private sealed class Counting : HttpMessageHandler
    {
        public int Requests;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        }
    }

    [Theory]
    [InlineData("https://json.tarkov.dev/pve/maps")]
    [InlineData("https://assets.tarkov.dev/maps/svg/Customs.svg")]
    [InlineData("https://ASSETS.tarkov.dev/maps/labs/main/2/1/1.png")]
    [InlineData("https://raw.githubusercontent.com/the-hideout/tarkov-dev/main/src/data/maps.json")]
    public async Task The_hosts_the_data_comes_from_are_asked(string address)
    {
        var transport = new Counting();
        using var client = CachedHttp.CreateClient(transport);
        Assert.Equal("ok", await client.GetStringAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal(1, transport.Requests);
    }

    [Theory]
    [InlineData("https://example.com/maps/svg/Customs.svg")] // another host
    [InlineData("http://assets.tarkov.dev/maps/svg/Customs.svg")] // not https
    [InlineData("https://assets.tarkov.dev.example.com/x.png")] // a look-alike
    [InlineData("https://192.168.1.1/admin")] // the local network
    [InlineData("http://localhost:8080/x")]
    [InlineData("file:///C:/Windows/win.ini")]
    public async Task No_other_address_is_asked(string address)
    {
        var transport = new Counting();
        using var client = CachedHttp.CreateClient(transport);
        await Assert.ThrowsAnyAsync<Exception>(() => client.GetStringAsync(address, TestContext.Current.CancellationToken));
        Assert.Equal(0, transport.Requests);
        Assert.False(CachedHttp.Allows(new Uri(address)));
    }

    [Fact]
    public void The_list_is_the_three_hosts_the_design_names() =>
        Assert.Equal(["assets.tarkov.dev", "json.tarkov.dev", "raw.githubusercontent.com"], CachedHttp.Hosts.Order());
}
