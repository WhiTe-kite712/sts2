using BaseLib.Abstracts;
using MySts2Mod.MySts2ModCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace MySts2Mod.MySts2ModCode.Cards;

/// <summary>
/// This is the base class for your mod's cards, which is set up to load the card's images from your mod's resources.
/// When creating a card, right click the Cards folder and create a new file with the Custom Card template.
/// This will generate a class that extends this one.
/// You can also just create the class manually; just make sure to inherit from this class.
/// </summary>
public abstract class MySts2ModCard(int cost, CardType type, CardRarity rarity, TargetType target, bool showInCardLibrary = true) :
    CustomCardModel(cost, type, rarity, target, showInCardLibrary)
{
    //Image size:
    // New card portraits are prepared at 1000x790; existing artwork keeps its dimensions.
    //Full art: 606x852
    public override string CustomPortraitPath => $"{GetType().Name}.png".BigCardImagePath();
    
    //Smaller variants of card images for efficiency:
    //Smaller variant of fullart: 250x350
    //Smaller variant of normalart: 250x190
    
    //Uses card_portraits/<class name>.png, preserving the filename's case (e.g. HaoStrike.png).
    public override string PortraitPath => $"{GetType().Name}.png".CardImagePath();
    public override string BetaPortraitPath => PortraitPath;
}
