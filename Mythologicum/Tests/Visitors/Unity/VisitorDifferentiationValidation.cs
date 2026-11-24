using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs the visitor interests, authored appeal, appearance and interface fixtures together.</summary>
public static class VisitorDifferentiationValidation
{
    public static void Run()
    {
        try
        {
            PlayerSettings.companyName = "MythologicumValidation";
            PlayerSettings.productName = "VisitorDifferentiationValidation";
            EditorSceneManager.OpenScene("Assets/Mythologicum/Scenes/MainMenuScene.unity");
            int appeal = CreatureAppealNativeChecks.RunChecks();
            int behavior = VisitorTypesNativeChecks.RunChecks();
            int people = VisitorPeopleUINativeChecks.RunChecks();
            File.WriteAllText("VisitorDifferentiationResults.txt", $"PASS: {appeal + behavior + people} native checks ({appeal} appeal, {behavior} behavior, {people} appearance/UI).");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("VisitorDifferentiationResults.txt", "FAIL: " + error);
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
}
