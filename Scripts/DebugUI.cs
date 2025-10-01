using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;

public partial class DebugUI : Node
{
    [Export] private SpiderMovement spiderMovement;
    [Export] private ProceduralWalk proceduralWalk;

    [Export] private Control fieldsContainer;
    [Export] private Control leftDebug;
    [Export] private CheckButton debugCheck;

    [Export] private Slider speedSlider;
    [Export] private Slider rotationSlider;
    [Export] private Label speedLabel;
    [Export] private Label rotationLabel;

    private Dictionary<FieldInfo, Label> fieldValues;

    public override void _Ready()
    {
        fieldValues = new Dictionary<FieldInfo, Label>();
        SetupUI();
    }

    public override void _Process(double delta)
    {
        if (debugCheck.ButtonPressed)
        {
            leftDebug.Visible = true;
        }
        else
        {
            leftDebug.Visible = false;
            return;
        }

        UpdateUI();

        spiderMovement.CurrentSpeed = (float)speedSlider.Value;
        spiderMovement.CurrentRotation = (float)rotationSlider.Value;

        speedLabel.Text = spiderMovement.CurrentSpeed.ToString();
        rotationLabel.Text = spiderMovement.CurrentRotation.ToString();
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
        foreach (KeyValuePair<FieldInfo, Label> kvp in fieldValues)
        {
            kvp.Value.Text = kvp.Key.GetValue(proceduralWalk).ToString();
        }
    }
}
