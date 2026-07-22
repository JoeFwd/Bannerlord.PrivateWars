using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attribute = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace Bannerlord.PrivateWars.UI.PrefabExtensions
{
    internal abstract class PartyNameplatePrivateWarFontColorPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new()
        {
            new Attribute("Brush.FontColor", "@PrivateWarFontColor")
        };
    }

    [PrefabExtension("PartyNameplateItem", "descendant::TextWidget[@Id='NameplateTextWidget']")]
    internal sealed class PartyNameplateTextWidgetPrivateWarFontColorPatch : PartyNameplatePrivateWarFontColorPatch
    {
    }

    [PrefabExtension("PartyNameplateItem", "descendant::TextWidget[@Id='NameplateExtraInfoTextWidget']")]
    internal sealed class PartyNameplateExtraInfoTextWidgetPrivateWarFontColorPatch : PartyNameplatePrivateWarFontColorPatch
    {
    }

    [PrefabExtension("PartyNameplateItem", "descendant::TextWidget[@Id='SpeedTextWidget']")]
    internal sealed class PartyNameplateSpeedTextWidgetPrivateWarFontColorPatch : PartyNameplatePrivateWarFontColorPatch
    {
    }

    [PrefabExtension("PartyNameplateItem", "descendant::TextWidget[@Id='NameplateFullNameTextWidget']")]
    internal sealed class PartyNameplateFullNameTextWidgetPrivateWarFontColorPatch : PartyNameplatePrivateWarFontColorPatch
    {
    }
}
