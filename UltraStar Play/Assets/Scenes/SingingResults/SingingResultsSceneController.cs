﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using UniInject;
using UnityEngine;
using UnityEngine.UI;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class SingingResultsSceneController : MonoBehaviour, INeedInjection, IBinder
{
    [InjectedInInspector]
    public Text songLabel;

    [InjectedInInspector]
    public GameObject onePlayerLayout;

    [InjectedInInspector]
    public GameObject twoPlayerLayout;

    [Inject]
    private Statistics statistics;

    [Inject]
    private Injector injector;

    private SingingResultsSceneData sceneData;

    public static SingingResultsSceneController Instance
    {
        get
        {
            return FindObjectOfType<SingingResultsSceneController>();
        }
    }

    void Start()
    {
        sceneData = SceneNavigator.Instance.GetSceneDataOrThrow<SingingResultsSceneData>();
        SelectLayout();
        FillLayout();
    }

    private void FillLayout()
    {
        SongMeta songMeta = sceneData.SongMeta;
        string titleText = (String.IsNullOrEmpty(songMeta.Title)) ? "" : songMeta.Title;
        string artistText = (String.IsNullOrEmpty(songMeta.Artist)) ? "" : " - " + songMeta.Artist;
        songLabel.text = titleText + artistText;

        int i = 0;
        GameObject selectedLayout = GetSelectedLayout();
        foreach (PlayerProfile playerProfile in sceneData.PlayerProfiles)
        {
            sceneData.PlayerProfileToMicProfileMap.TryGetValue(playerProfile, out MicProfile micProfile);
            PlayerScoreControllerData playerScoreData = sceneData.GetPlayerScores(playerProfile);
            SongRating songRating = GetSongRating(playerScoreData.TotalScore);

            Injector childInjector = UniInjectUtils.CreateInjector(injector);
            childInjector.AddBindingForInstance(playerProfile);
            childInjector.AddBindingForInstance(micProfile);
            childInjector.AddBindingForInstance(playerScoreData);
            childInjector.AddBindingForInstance(songRating);
            childInjector.AddBinding(new Binding("playerProfileIndex", new ExistingInstanceProvider<int>(i)));

            SingingResultsPlayerUiController[] uiControllers = selectedLayout.GetComponentsInChildren<SingingResultsPlayerUiController>();
            if (i < uiControllers.Length)
            {
                childInjector.InjectAllComponentsInChildren(uiControllers[i]);
            }
            i++;
        }
    }

    private void SelectLayout()
    {
        int playerCount = sceneData.PlayerProfiles.Count;
        List<GameObject> layouts = new List<GameObject>();
        layouts.Add(onePlayerLayout);
        layouts.Add(twoPlayerLayout);

        GameObject selectedLayout = GetSelectedLayout();
        foreach (GameObject layout in layouts)
        {
            layout.SetActive(layout == selectedLayout);
        }
    }

    private GameObject GetSelectedLayout()
    {
        int playerCount = sceneData.PlayerProfiles.Count;
        if (playerCount == 2)
        {
            return twoPlayerLayout;
        }
        return onePlayerLayout;
    }

    public void FinishScene()
    {
        if (statistics.HasHighscore(sceneData.SongMeta))
        {
            // Go to highscore scene
            HighscoreSceneData highscoreSceneData = new HighscoreSceneData();
            highscoreSceneData.SongMeta = sceneData.SongMeta;
            highscoreSceneData.Difficulty = sceneData.PlayerProfiles.FirstOrDefault().Difficulty;
            SceneNavigator.Instance.LoadScene(EScene.HighscoreScene, highscoreSceneData);
        }
        else
        {
            // No highscores to show, thus go to song select scene
            SongSelectSceneData songSelectSceneData = new SongSelectSceneData();
            songSelectSceneData.SongMeta = sceneData.SongMeta;
            SceneNavigator.Instance.LoadScene(EScene.SongSelectScene, songSelectSceneData);
        }

    }

    public List<IBinding> GetBindings()
    {
        BindingBuilder bb = new BindingBuilder();
        bb.BindExistingInstance(this);
        bb.BindExistingInstance(sceneData);
        return bb.GetBindings();
    }

    private SongRating GetSongRating(double totalScore)
    {
        foreach (SongRating songRating in SongRating.Values)
        {
            if (totalScore > songRating.ScoreThreshold)
            {
                return songRating;
            }
        }
        return SongRating.ToneDeaf;
    }
}
