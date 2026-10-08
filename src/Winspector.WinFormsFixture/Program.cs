using System;
using System.Drawing;
using System.Windows.Forms;

namespace Winspector.WinFormsFixture;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new Form { Text = "Winspector .NET Framework 4.8 fixture", Name = "FixtureWindow", Size = new Size(650, 400) };
        var panel = new Panel { Name = "DynamicPanel", Location = new Point(20, 90), Size = new Size(590, 230), BorderStyle = BorderStyle.FixedSingle };
        var dynamic = new Button { Name = "DynamicButton", Text = "Dynamic control", Location = new Point(20, 30), Size = new Size(160, 35) };
        var add = new Button { Name = "AddControl", Text = "Add control", Location = new Point(20, 20), Size = new Size(110, 35) };
        var hide = new Button { Name = "ToggleVisibility", Text = "Show / hide", Location = new Point(140, 20), Size = new Size(110, 35) };
        var remove = new Button { Name = "RemoveControl", Text = "Remove control", Location = new Point(260, 20), Size = new Size(110, 35) };
        var dialog = new Button { Name = "OpenDialog", Text = "Open dialog", Location = new Point(380, 20), Size = new Size(110, 35) };
        add.Click += (_, _) => { if (!panel.Controls.Contains(dynamic)) panel.Controls.Add(dynamic); dynamic.Visible = true; };
        hide.Click += (_, _) => dynamic.Visible = !dynamic.Visible;
        remove.Click += (_, _) => panel.Controls.Remove(dynamic);
        dialog.Click += (_, _) => new Form { Name = "ExtraWindow", Text = "Additional target window", Size = new Size(320, 180) }.Show(form);
        form.Controls.AddRange(new Control[] { add, hide, remove, dialog, panel });
        Application.Run(form);
        dynamic.Dispose();
    }
}