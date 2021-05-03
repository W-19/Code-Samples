/*
 * This file controls a simple developer console I made for Trials of Tonalli. It attempts to parse a string
 * representing a command and either calls a function to execute the command or prints an appropriate error
 * message. While it's perhaps not the most robust command parser ever developed (it only supports a single
 * line of output text and has no support for autocomplete), it's simple and easily expandable and served its
 * purpose of allowing me to test edge cases which could lead to undesired behavior during normal gameplay.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConsoleCommands : MonoBehaviour
{
    public bool consoleEnabled = true;
    [HideInInspector]
    public GameObject console;

    public UnityEngine.UI.InputField inputField;
    public UnityEngine.UI.Text logText;

    private ItemData itemData;

    private GameObject keyboardPlayerInputObj = null;

    void Start(){
        itemData = GameObject.Find("InventoryManager").GetComponent<ItemData>();

        foreach(GameObject playerInputObj in GameObject.FindGameObjectsWithTag("LobbyPlayer")){
            if(playerInputObj.GetComponent<UnityEngine.InputSystem.PlayerInput>().currentControlScheme == "Keyboard"){
                keyboardPlayerInputObj = playerInputObj;
                break;
            }
        }
    }

    void Update(){
        if(consoleEnabled && Input.GetKeyDown(KeyCode.BackQuote)){
            console.SetActive(!console.activeSelf);
        }

        // If we're typing in the console, disable input for the keyboard player
        if(keyboardPlayerInputObj != null){
            keyboardPlayerInputObj.SetActive(!(console.activeSelf && inputField.isFocused));
        }
    }

    // For now, called whenever focus is moved off the text field (including when enter is hit). Oh well.
    public void OnCommandEnter(){
        if(inputField.text == "") return;

        string[] words = inputField.text.Split(' ');
        string command = words[0];
        // This is a dumb way to do it but w/e
        string[] args = new string[words.Length-1];
        for(int i = 1; i < words.Length; i++){
            args[i-1] = words[i];
        }

        try{
            switch(command){
                case "give": Give(GetPlayers(args[0]), args[1], args.Length < 3 ? 1 : Int16.Parse(args[2])); break;
                case "clear": Clear(GetPlayers(args[0])); break;
                case "kill": Kill(GetPlayers(args[0])); break;
                case "restart": Restart(); break;
                case "quit": QuitToMenu(); break;
                case "help": logText.text = "This is a buggy developer console. Commands:\ngive\nclear\nkill\nrestart\nquit\nhelp"; break;
                default: logText.text = "Unrecognized command: " + command; break;
            }
        }
        catch(ArgumentException e){
            logText.text = e.Message;
        }
        catch(Exception){
            logText.text = "Malformed command!";
        }

        inputField.text = "";
    }

    // ------------------------------------------------------------------------------------------------------

    private void Give(GameObject[] players, string itemType, int amount){
        if(amount <= 0) throw new ArgumentException("Invalid amount specified!");

        Item item = null;
        switch(itemType){
            case "random_relic": item = itemData.GetRandomRelic(); break;
            case "urel":
            case "universal_relic": item = itemData.universalRelic; break;
            case "random_spell": item = itemData.GetRandomSpell(); break;
            case "hook": item = itemData.spells[0]; break;
            case "barrier": item = itemData.spells[1]; break;
            case "steal":
            case "relic_thief": item = itemData.spells[2]; break;
            default: logText.text = "Invalid item specified!"; return;
        }

        foreach(GameObject player in players){
            PlayerInventory playerInv = player.GetComponent<PlayerInventory>();
            for(int i = 0; i < amount; i++){
                playerInv.AddItemToInventory(item);
            }
        }

        logText.text = "Gave " + item.name + " x" + amount + " to " + (players.Length == 1 ? players[0].name : "all players");
    }

    private void Clear(GameObject[] players){
        foreach(GameObject player in players){
            player.GetComponent<PlayerInventory>().Clear();
        }

        logText.text = "Cleared " + (players.Length == 1 ? players[0].name + "'s inventory" : "all players' inventories");
    }

    private void Kill(GameObject[] players){
        foreach(GameObject player in players){
            player.GetComponent<PlayerRespawn>().Kill();
        }

        logText.text = "Killed " + (players.Length == 1 ? players[0].name : "all players");
    }

    private void Restart(){
        logText.text = "Restarting...";
        SceneChangeScript.Restart();
    }

    private void QuitToMenu(){
        logText.text = "Quitting to main menu...";
        keyboardPlayerInputObj.SetActive(true); // So FindGameObjectsWithTag will see it and we can destroy it
        SceneChangeScript.Quit();
    }

    // ------------------------------------------------------------------------------------------------------

    private GameObject[] GetPlayers(string playerSelector){
        if(playerSelector == "all") return GameObject.FindGameObjectsWithTag("Player");
        GameObject player = GameObject.Find("Player " + playerSelector);
        if(player != null) return new GameObject[]{player};
        throw new ArgumentException("Invalid player specified!");
    }
}
