namespace MDI.Philips.M1350.Tests;

[TestClass]
public sealed class ProtocolRevisionTests
{
    [TestMethod]
    public void ParseShouldReturnRevisionForValidToken()
    {
        ProtocolRevision revision = ProtocolRevision.Parse("A20", provider: null);

        Assert.AreEqual('A', revision.Generation);
        Assert.AreEqual((byte)2, revision.Minor);
        Assert.AreEqual((byte)0, revision.Patch);
    }

    [TestMethod]
    public void SpanParseShouldReturnRevisionForValidToken()
    {
        ProtocolRevision revision = ProtocolRevision.Parse("B31".AsSpan(), provider: null);

        Assert.AreEqual('B', revision.Generation);
        Assert.AreEqual((byte)3, revision.Minor);
        Assert.AreEqual((byte)1, revision.Patch);
    }

    [TestMethod]
    public void ParseShouldThrowForInvalidToken()
    {
        Assert.ThrowsExactly<FormatException>(() => ProtocolRevision.Parse("A.02.00", provider: null));
    }

    [TestMethod]
    public void TryParseShouldReturnFalseForNullString()
    {
        bool result = ProtocolRevision.TryParse(null, provider: null, out ProtocolRevision revision);

        Assert.IsFalse(result);
        Assert.AreEqual(default, revision);
    }

    [TestMethod]
    public void TryParseShouldReturnFalseForLowercaseGeneration()
    {
        bool result = ProtocolRevision.TryParse("a20", provider: null, out ProtocolRevision revision);

        Assert.IsFalse(result);
        Assert.AreEqual(default, revision);
    }

    [TestMethod]
    public void ComparisonOperatorsShouldOrderRevisions()
    {
        ProtocolRevision older = ProtocolRevision.Parse("A20", provider: null);
        ProtocolRevision newer = ProtocolRevision.Parse("A21", provider: null);

        Assert.IsTrue(newer > older);
        Assert.IsTrue(older < newer);
        Assert.IsTrue(newer >= older);
        Assert.IsTrue(older <= newer);
    }

    [TestMethod]
    public void ToStringShouldRoundTripWireToken()
    {
        ProtocolRevision revision = new('A', 2, 0);

        Assert.AreEqual("A20", revision.ToString());
    }
}
