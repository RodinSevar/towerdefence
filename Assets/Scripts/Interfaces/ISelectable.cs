using UnityEngine;

public interface ISelectable
{
    string GetDisplayName();
    string GetStatsText();
    bool IsSellable();
    int GetRefundAmount();
    void Sell();
    void SetSelected(bool isSelected);
}
