/*
 * This script drives the functionality for the in-game menu and crafting panel in Poly Dungeon Runner
 * (project name subject to change). My goal is to make all menu/crafting elements highlightable and
 * clickable with both the "Keyboard & Mouse" and "Gamepad" control schemes. While the crafting menu
 * doesn't have full gamepad support yet, I'm pretty happy with how this script is coming along so far.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class IGM_Controller : MonoBehaviour
{
    enum Direction
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4
    }

    public Player player;

    public IGM_Element selected;
    public Gradient selectedElementGradient;
    public GameObject selectedItemOutline;

    public IGM_Element craftingPanelFirstSelected;
    public IGM_Element pauseMenuFirstelected;
    public IGM_Element deathMenuFirstSelected;

    private GameObject currentMenuObj;
    private IGM_Element[] activeMenuElements;

    public static bool paused = false;

    // If an element is selected, animate the element's color
    void Update(){
        if(selected == null){
            selectedItemOutline.SetActive(false);
            return;
        }

        Color currentColor = selectedElementGradient.Evaluate((float)Math.Sin(4f*Time.unscaledTime)/2f + 0.5f);

        if(selected is IGM_Item){
            selectedItemOutline.SetActive(true);
            selectedItemOutline.transform.position = selected.transform.position;
            selectedItemOutline.GetComponent<UnityEngine.UI.Image>().color = currentColor;
        }
        else{
            selectedItemOutline.SetActive(false);
            if(selected is IGM_Text){
                selected.GetComponent<UnityEngine.UI.Text>().color = currentColor;
            }
        }
    }

    // ------------------------------------ INPUT METHODS ------------------------------------

    public void OnPoint(InputValue value){
        if(currentMenuObj == null) return;

        UpdateSelected(GetElementUnderCursor());
    }

    public void OnClick(InputValue value){
        if(currentMenuObj == null) return;

        IGM_Element clickedElement = GetElementUnderCursor();
        if(clickedElement!= null) clickedElement.SubmitAction();
    }

    public void OnNavigate(InputValue value){
        if(currentMenuObj == null) return;
        if(selected == null){
            selected = activeMenuElements[0];
            return;
        }

        Vector2 valueVector = value.Get<Vector2>();
        if(valueVector == Vector2.up) Navigate(Direction.Up);
        else if(valueVector == Vector2.down) Navigate(Direction.Down);
        else if(valueVector == Vector2.left) Navigate(Direction.Left);
        else if(valueVector == Vector2.right) Navigate(Direction.Right);
    }

    public void OnSubmit(InputValue value){
        if(selected == null) return;

        selected.SubmitAction();
    }

    // The toggle pause key can also be used to escape from the crafting panel
    public void OnPause(InputValue value){
        if(player.IsDead()) return;

        if(!paused && currentMenuObj != null) ReturnToGameplay();
        else TogglePause();
    }

    public void OnToggleCraftingPanel(){
        if(paused || player.IsDead()) return;

        if(currentMenuObj == null){
            LoadMenu(craftingPanelFirstSelected);
            TimeController.SetTimeScale(0.05f); // TODO: Account for in-game changes to the time scale
        }
        else{
            ReturnToGameplay();
        }
    }

    // ------------------------------ PRIVATE AUXILIARY METHODS ------------------------------

    private IGM_Element GetElementUnderCursor(){
        Vector2 pointerPos = Pointer.current.position.ReadValue();
        foreach(IGM_Element element in activeMenuElements){
            if(element.IsPointerOverGameObject(pointerPos)){
                return element;
            }
        }
        return null;
    }

    // TODO: Make this function work with items too
    private void Navigate(Direction dir){
        if(selected is IGM_Text selectedElem){
            if(dir == Direction.Up && selectedElem.up != null) UpdateSelected(selectedElem.up);
            else if(dir == Direction.Down && selectedElem.down != null) UpdateSelected(selectedElem.down);
            else if(dir == Direction.Left && selectedElem.left != null) UpdateSelected(selectedElem.left);
            else if(dir == Direction.Right && selectedElem.right != null) UpdateSelected(selectedElem.right);
        }
    }

    private void UpdateSelected(IGM_Element newSelected){
        if(selected != null && selected is IGM_Text selectedElem){
            selectedElem.gameObject.GetComponent<UnityEngine.UI.Text>().color = Color.white;
        }
        selected = newSelected;
    }

    private void LoadMenu(IGM_Element firstSelected){
        if(firstSelected is IGM_Text){
            currentMenuObj = firstSelected.transform.parent.gameObject;
        }
        else{
            currentMenuObj = firstSelected.transform.parent.parent.gameObject;
        }
        currentMenuObj.SetActive(true);

        // Only select something by default if we're using a controller
        if(player.input.currentControlScheme != "Keyboard&Mouse"){
            UpdateSelected(firstSelected);
        }

        // Create an array of active menu elements when we load a menu so we can select elements with the cursor
        activeMenuElements = currentMenuObj.GetComponentsInChildren<IGM_Element>();
    }

    // ------------- PUBLIC METHODS - May be called from here or by other scripts -------------

    public void PlayerDeath(){
        LoadMenu(deathMenuFirstSelected);
    }

    // Public so it can be called from the "Return to Game" menu element
    public void TogglePause(){
        if(paused){
            ReturnToGameplay();
            paused = false;
        }
        else if(player.IsDead() == false){
            paused = true;
            TimeController.SetTimeScale(0f);
            LoadMenu(pauseMenuFirstelected);
        }
    }

    // Called by TogglePause, OnToggleCraftingPanel and from the "Escape" IGM_Item in the crafting panel
    public void ReturnToGameplay(){
        UpdateSelected(null);
        currentMenuObj.SetActive(false);
        currentMenuObj = null;
        activeMenuElements = null;
        TimeController.SetTimeScale(1f);
    }

    // Could be put in IGM_Element but makes more sense here imo
    public static void ExitToMenu(){
        paused = false;
        TimeController.SetTimeScale(1f);
        SceneManager.LoadScene("Menu");
    }
}
