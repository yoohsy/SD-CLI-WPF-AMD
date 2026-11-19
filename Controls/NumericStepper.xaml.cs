using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CdCliWpf.Controls;

public partial class NumericStepper : UserControl
{
    private bool _updatingText;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(NumericStepper),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(NumericStepper),
        new FrameworkPropertyMetadata(0d, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(NumericStepper),
        new FrameworkPropertyMetadata(100d, OnRangeChanged));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(NumericStepper),
        new FrameworkPropertyMetadata(1d));

    public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
        nameof(Decimals), typeof(int), typeof(NumericStepper),
        new FrameworkPropertyMetadata(0, OnDecimalsChanged));

    public NumericStepper()
    {
        InitializeComponent();
        PART_TextBox.TextChanged += PART_TextBox_TextChanged;
        PART_TextBox.LostFocus += (_, _) => CommitText();
        PART_TextBox.KeyDown += PART_TextBox_KeyDown;
        Loaded += (_, _) => UpdateText();
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public int Decimals
    {
        get => (int)GetValue(DecimalsProperty);
        set => SetValue(DecimalsProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NumericStepper)d).UpdateText();

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var stepper = (NumericStepper)d;
        stepper.CoerceValue(ValueProperty);
        stepper.UpdateText();
    }

    private static void OnDecimalsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NumericStepper)d).UpdateText();

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var stepper = (NumericStepper)d;
        var value = baseValue is double number && !double.IsNaN(number) ? number : stepper.Minimum;
        return Math.Min(stepper.Maximum, Math.Max(stepper.Minimum, value));
    }

    private void PART_TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingText)
            return;

        var raw = PART_TextBox.Text.Trim().Replace(',', '.');
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            Value = parsed;
    }

    private void PART_TextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        CommitText();
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void CommitText()
    {
        var raw = PART_TextBox.Text.Trim().Replace(',', '.');
        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            Value = parsed;
        else
            UpdateText();
    }

    private void UpdateText()
    {
        if (_updatingText || PART_TextBox is null || PART_TextBox.IsFocused)
            return;

        _updatingText = true;
        try
        {
            var format = Decimals > 0 ? "F" + Decimals.ToString(CultureInfo.InvariantCulture) : "0";
            PART_TextBox.Text = Value.ToString(format, CultureInfo.InvariantCulture);
        }
        finally
        {
            _updatingText = false;
        }
    }

    private void PART_Up_Click(object sender, RoutedEventArgs e) => Value += Step;

    private void PART_Down_Click(object sender, RoutedEventArgs e) => Value -= Step;
}
