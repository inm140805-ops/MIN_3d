using UnityEngine;
using UnityEngine.EventSystems;

public sealed class LaundryButtonHover : MonoBehaviour, IPointerEnterHandler
{
    private ShopMenuStyle menu;
    private int index;
    private bool pause;

    public void Configure(ShopMenuStyle owner, int buttonIndex, bool isPauseButton)
    {
        menu = owner;
        index = buttonIndex;
        pause = isPauseButton;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (menu == null) return;
        if (pause) menu.SelectPauseButton(index);
        else menu.SelectIntroButton(index);
    }
}
