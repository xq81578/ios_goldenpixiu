using UnityEngine;

[CreateAssetMenu(fileName = "GlobalSettings", menuName = "ScriptableObjects/GlobalSettings", order = 1)]
public class GlobalSettings : ScriptableObject
{
    [Header("Graphics Settings")]
    public bool FullScreen = true;
    public int ScreenWidth = 1920;
    public int ScreenHeight = 1080;
    public int DesktopFPS = 60;
    public int MobileFPS = 60;

    [Header("Audio Settings")]
    [Range(0, 1)]
    public float MasterVolume = 1.0f;
    [Range(0, 1)]
    public float MusicVolume = 0.5f;
    [Range(0, 1)]
    public float SFXVolume = 0.5f;

    [Header("Gameplay Settings")]
    public bool InvertYAxis = false;
    public float Sensitivity = 1.0f;
}
