using System;

namespace PixUI.Dynamic.Design;

public sealed class ColorEditor : SingleChildWidget
{
    public ColorEditor(State<Color?> color)
    {
        _color = color;

        State<Color> colorState = color.ToNoneNullable(Colors.Black);
        State<IconData> iconState =
            RxComputed<IconData>.Make(color, v => v.HasValue ? MaterialIcons.Square : MaterialIcons.Clear);

        Child = new Button(icon: iconState) { TextColor = colorState, Style = ButtonStyle.Outline, OnTap = OnTap };
    }

    private readonly State<Color?> _color;

    private void OnTap(PointerEvent e)
    {
        //TODO: test only
        _color.Value = Colors.Random();
    }
}