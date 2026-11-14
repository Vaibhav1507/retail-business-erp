namespace Rbos.UnitTests;

public class PdfPageReaderTests
{
    [Fact]
    public void DetectsManufacturerPdfPagesWithCorruptedEmbeddedFontMaps()
    {
        const string garbled = """
            'LVSOD\ 1DQR&RORU.
            3URFHVVRU DOSKD  $, 3URFHVVRU . *HQ 
            3LFWXUH $, 6XSHU 8SVFDOLQJ 'DQDPLF 7RQH 0DSSLQJ
            6RXQG $, 6RXQG 3UR 9LUWXDO ..FK
            6SHDNHU .FK / :
            0RYLH 'ROE$WPRV
            &RQQHFWLYLW +'0, . :L)L %OXHWRRWK
            $, 0DJLF 5HPRWH $, &RQFLHUJH
            ZHE26 
            """;

        Assert.True(Rbos.Api.Application.PdfPageReader.IsUnreliableText(garbled));
    }

    [Fact]
    public void KeepsNormallyExtractedManufacturerSpecificationPages()
    {
        const string readable = """
            Display Nano Color 4K
            Processor alpha 7 AI Processor 4K Gen9
            Picture AI Super Upscaling, Dynamic Tone Mapping, AI HDR Remastering
            Sound AI Sound Pro, Virtual 9.1.2 channel
            Speaker 2.0 channel, 20W
            Movie Dolby Atmos, FILMMAKER MODE, HDR10 Pro
            Game Game Dashboard, HGiG, ALLM, eARC
            Connectivity HDMI 2.0, Wi-Fi, Bluetooth
            Operating System webOS 26
            Remote AI Magic Remote
            """;

        Assert.False(Rbos.Api.Application.PdfPageReader.IsUnreliableText(readable));
    }

    [Fact]
    public void TreatsPagesWithoutAnExtractableTextLayerAsNeedingOcr()
    {
        Assert.True(Rbos.Api.Application.PdfPageReader.IsUnreliableText(""));
        Assert.True(Rbos.Api.Application.PdfPageReader.IsUnreliableText("Only a few words"));
    }
}
