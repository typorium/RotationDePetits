using Newtonsoft.Json;
using NSMB.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NSMB.UI.MainMenu.Submenus.Main {
    public class NewsBoardManager : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private NewsBoardEntry template;
        [SerializeField] private GameObject loading;

        //---Private Variables
        private List<NewsBoardEntry> posts = new();
        private bool gotPosts;

        // Handmade news data
         private static readonly List<NewsBoardEntry.NewsBoardData> handmadeNews = new() {
            new NewsBoardEntry.NewsBoardData {
                Id = 1,
                Title = "Bienvenue sur Rotation De Parpaing !",
                Text = "En gros c'est notre jeu moddé rigolo voila\n Maps & Traduction RDP : Luden \nCode et Modes de jeu : Typorium",
                Created = DateTimeOffset.Parse("2026-05-26").ToUnixTimeSeconds()
            },
        };

        public void OnEnable() {
            if (!gotPosts) {
                loading.SetActive(true);

                foreach (var postData in handmadeNews) {
                    NewsBoardEntry newPost = Instantiate(template, template.transform.parent);
                    newPost.Initialize(postData);
                    posts.Add(newPost);
                }

                gotPosts = true;
                loading.SetActive(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)template.transform.parent);
            }
        }
    }
}
