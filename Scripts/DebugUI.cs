using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class DebugUI : Node
{
    [Export] private ProceduralWalk proceduralWalk;
    [Export] private Control fieldsContainer;
    [Export] private Control leftDebug;
    [Export] private CheckButton debugCheck;

    private Dictionary<FieldInfo, Label> fieldValues;

    public override void _Ready()
    {
        fieldValues = new Dictionary<FieldInfo, Label>();
        SetupUI();
    }

    public override void _Process(double delta)
    {
        UpdateUI();

        if (debugCheck.ButtonPressed)
        {
            leftDebug.Visible = true;
        }
        else
        {
            leftDebug.Visible = false;
        }
    }

    private void SetupUI()
    {
        var fields = proceduralWalk.GetType().GetFields(
            BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            bool debug = Attribute.IsDefined(field, typeof(DebugAttribute));

            if (!debug)
            {
                continue;
            }

            HBoxContainer hBoxContainer = new HBoxContainer();

            Label fieldLabel = new Label();
            fieldLabel.Text = field.Name;

            Label fieldValue = new Label();
            fieldValue.Text = field.GetValue(proceduralWalk).ToString();

            hBoxContainer.AddChild(fieldLabel);
            hBoxContainer.AddChild(fieldValue);

            fieldsContainer.AddChild(hBoxContainer);

            fieldValues.Add(field, fieldValue);
        }
    }

    private void UpdateUI()
    {
        if (!debugCheck.ButtonPressed)
        {
            return;
        }
        foreach (KeyValuePair<FieldInfo, Label> kvp in fieldValues)
        {
            kvp.Value.Text = kvp.Key.GetValue(proceduralWalk).ToString();
        }
    }
}
