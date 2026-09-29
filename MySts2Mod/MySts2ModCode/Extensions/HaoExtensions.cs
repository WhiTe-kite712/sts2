using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Extensions;

/// <summary>
/// 豪意值的读写辅助。卡牌代码统一通过这些方法操作豪意，便于以后集中调整规则。
/// </summary>
public static class HaoExtensions
{
    public static int GetHao(this Player player)
    {
        return player.Creature.GetPower<HaoPower>()?.Amount ?? 0;
    }

    public static async Task GainHao(this Player player, PlayerChoiceContext choiceContext, int amount, CardModel? cardSource = null)
    {
        if (amount <= 0) return;
        await PowerCmd.Apply<HaoPower>(choiceContext, player.Creature, amount, player.Creature, cardSource);
    }

    public static async Task<int> LoseHao(this Player player, PlayerChoiceContext choiceContext, int amount, CardModel? cardSource = null)
    {
        var power = player.Creature.GetPower<HaoPower>();
        if (power == null || amount <= 0) return 0;

        var lost = Math.Min(amount, power.Amount);
        await PowerCmd.ModifyAmount(choiceContext, power, -lost, player.Creature, cardSource);
        return lost;
    }
}
