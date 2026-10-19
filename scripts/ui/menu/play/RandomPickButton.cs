using System;
using Godot;

public partial class RandomPickButton : Button
{
    private Random random = new();

    public override void _Pressed()
    {
        Pick();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && key.CtrlPressed)
        {
            switch (key.Keycode)
            {
                case Key.F4:
                    Pick();
                    break;
            }
        }
    }

    public void Pick()
    {
        var mapList = MapList.Instance;

        // Random.Next(0) returns 0 rather than throwing, so an empty list would otherwise
        // fault on Maps[0].
        if (mapList == null || mapList.Maps.Count == 0)
        {
            _ = ToastNotification.Notify(L10n.Tr("There are no maps to pick from"), 1);
            return;
        }

        int index = random.Next(mapList.Maps.Count);

        mapList.Select(mapList.Maps[index], false);
    }
}
