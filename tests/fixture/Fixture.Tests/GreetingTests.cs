using Xunit;

public class GreetingTests
{
    [Fact]
    public void Text_Greets()
    {
        Assert.Equal("Hello, World!", Greeting.Text);
    }
}
