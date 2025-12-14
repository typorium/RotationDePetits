namespace Quantum.Editor {
  using System;
  using UnityEditor;
  using Photon.Deterministic;
  using UnityEngine;

  /// <summary>
  /// Debug windows that converts between double and FP values.
  /// </summary>
  public class QuantumEditorFPConverterWindow : EditorWindow {
    string _valueString;
    double _valueDouble;
    long _valueRaw;

    [MenuItem("Tools/Quantum/Window/FP Converter", priority = (int)QuantumEditorMenuPriority.Window + 30)]
    public static void ShowWindow() {
      GetWindowWithRect<QuantumEditorFPConverterWindow>(new Rect(0, 0, 400, 130), false, "Quantum FP Converter");
    }

    public virtual void OnGUI() {
      try {
        var newValueString = EditorGUILayout.TextField("String", _valueString);
        if (_valueString != newValueString) {
          _valueString = newValueString;
          _valueRaw = FP.FromString(_valueString).RawValue;
          _valueDouble = FP.FromRaw(_valueRaw).AsRoundedDouble;
        }

        var rect = EditorGUILayout.GetControlRect(true);
        var newValueDouble = EditorGUI.DoubleField(rect, "FP", _valueDouble);
        QuantumEditorGUI.Overlay(rect, "(FP)");
        if (newValueDouble != _valueDouble) {
          _valueDouble = FP.FromDouble_UNSAFE(newValueDouble).AsRoundedDouble;
          _valueRaw = FP.FromDouble_UNSAFE(_valueDouble).RawValue;
          _valueString = FP.FromRaw(_valueRaw).ToString();
        }

        var newValueRaw = EditorGUILayout.LongField("Raw", _valueRaw);
        if (newValueRaw != _valueRaw) {
          _valueRaw = newValueRaw;
          _valueString = FP.FromRaw(_valueRaw).ToString();
          _valueDouble = FP.FromRaw(_valueRaw).AsRoundedDouble;
        }

        GUI.enabled = false;
        EditorGUILayout.DoubleField("Double", FP.FromRaw(_valueRaw).AsDouble);
        GUI.enabled = true;

        if (_valueRaw > FP.Raw.UseableMax || _valueRaw < FP.Raw.UseableMin) {
          EditorGUILayout.HelpBox($"FP value is out of useable range [{FP.UseableMin} ... {FP.UseableMax}]", MessageType.Warning);
        }
      } catch (OverflowException e) {
        Log.Error(e.Message);
      } catch (FormatException e) {
        Log.Error(e.Message);
        _valueRaw = 0;
        _valueDouble = 0;
      } catch (Exception e) {
        Log.Error(e.Message);
      }
    }
  }
}