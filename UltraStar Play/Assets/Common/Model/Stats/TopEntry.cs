﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Holds data about one of the "top" songs
[Serializable]
public class TopEntry
{
    public string songName { get; private set; }
    public string songArtist { get; private set; }
    public SongStatistic songStatistic { get; private set; }

    public TopEntry(string songName, string songArtist, SongStatistic songStatistic)
    {
        this.songName = songName;
        this.songArtist = songArtist;
        this.songStatistic = songStatistic;
    }
}
