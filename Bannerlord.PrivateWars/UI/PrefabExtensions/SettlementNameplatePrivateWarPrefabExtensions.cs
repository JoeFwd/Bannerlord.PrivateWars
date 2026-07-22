using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;
using Attribute = Bannerlord.UIExtenderEx.Prefabs2.PrefabExtensionSetAttributePatch.Attribute;

namespace Bannerlord.PrivateWars.UI.PrefabExtensions
{
    internal abstract class SettlementNameplatePrivateWarCapsuleColorPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new()
        {
            new Attribute("Color", "@SettlementCapsuleColor")
        };
    }

    internal abstract class SettlementNameplatePrivateWarRelationPatch : PrefabExtensionSetAttributePatch
    {
        public override List<Attribute> Attributes => new()
        {
            new Attribute("RelationType", "@SettlementCapsuleRelationType")
        };
    }

    [PrefabExtension("SettlementNameplateItemSmall", "descendant::SettlementNameplateItemWidget[@Id='NameplateWidget']")]
    internal sealed class SmallSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItemMedium", "descendant::SettlementNameplateItemWidget[@Id='NameplateWidget']")]
    internal sealed class MediumSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItemLarge", "descendant::SettlementNameplateItemWidget[@Id='NameplateWidget']")]
    internal sealed class LargeSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItem", "descendant::SettlementNameplateItemWidget[@Id='SmallSizeNameplateWidget']")]
    internal sealed class CombinedSmallSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItem", "descendant::SettlementNameplateItemWidget[@Id='NormalSizeNameplateWidget']")]
    internal sealed class CombinedNormalSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItem", "descendant::SettlementNameplateItemWidget[@Id='BigSizeNameplateWidget']")]
    internal sealed class CombinedBigSettlementNameplatePrivateWarCapsuleColorPatch : SettlementNameplatePrivateWarCapsuleColorPatch
    {
    }

    [PrefabExtension("SettlementNameplateItemSmall", "descendant::SettlementNameplateWidget")]
    internal sealed class SmallSettlementNameplatePrivateWarRelationPatch : SettlementNameplatePrivateWarRelationPatch
    {
    }

    [PrefabExtension("SettlementNameplateItemMedium", "descendant::SettlementNameplateWidget")]
    internal sealed class MediumSettlementNameplatePrivateWarRelationPatch : SettlementNameplatePrivateWarRelationPatch
    {
    }

    [PrefabExtension("SettlementNameplateItemLarge", "descendant::SettlementNameplateWidget")]
    internal sealed class LargeSettlementNameplatePrivateWarRelationPatch : SettlementNameplatePrivateWarRelationPatch
    {
    }

    [PrefabExtension("SettlementNameplateItem", "descendant::SettlementNameplateWidget")]
    internal sealed class CombinedSettlementNameplatePrivateWarRelationPatch : SettlementNameplatePrivateWarRelationPatch
    {
    }
}
