/*
 * Used to control the crafting/main menu/hub mode 
 * Loads up new tiers of elements when the buttons on the side are clicked
 * Calls the tutorials on load if the conditions are met
 * Keeps the element panels updated
 */

//#define DEBUG
//#define IN_PROGRESS
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

//Script to create the element spawners and set up the crafting scene
//also used to control and update the elemnt and tier panels
 public class MainMenuController : MonoBehaviour {
	//events
	public delegate void ButtonPressAction();
	public delegate void EnterMenuScreenAction();
	public delegate void CallTutorialEventAction(TutorialType tutorial);
	public delegate void LoadTierAction(int tier);
	public static event ButtonPressAction OnButtonPress;
	public static event EnterMenuScreenAction OnEnterMenu;
	public static event CallTutorialEventAction OnCallTutorialEvent;
	public static event LoadTierAction OnLoadTier;

	//GameObject references to create the element spawners
	public CraftingButtonController buttonControl;
	public GameObject [] elementPanels;
	public GameObject [] tierButtons;
	public GameObject hintPanel;
	//public reference to the current element and panel
	public string activeElement;
	public Vector3 activePosition;

	//the overall percent unlocked 
	public Text numberUnlocked;
	public Text totalNumber;
	public Sprite [] unlockProgressSprites;
	public Image unlockProgress;

	//references to the tier and element panel scripts
	private TierButtonDisplay [] tierButtonScript;
	private SpawnerControl [] elementPanelControllers;

	//tier info 
	private int currentlySelectedTierNumber = -1;

	//bool to track whether load tier has been called before
	private bool firstCall = true;

	//tutorial variabes
	private PurchaseHint tutorialHint;

	void Awake () {
		//sets the global script reference
		GlobalVars.CRAFTING_CONTROLLER = this;

		//initializes the powerup sprites
		GlobalVars.InitializePowerUpSprites();

		//increases the play count for crafting
		GlobalVars.CRAFTING_PLAY_COUNT++;
	}

	void Start () {
		//calls the event
		if (OnEnterMenu != null) {
			OnEnterMenu();
		}

		//sets the text of the unlocked progres total to the count of elements
		totalNumber.text = "/" + GlobalVars.ELEMENTS.Count;

		//to load in images
		string filePath = GlobalVars.FILE_PATH;
	
		//loads the sprites for an element without an image and element that is locked
		Sprite noImage = Resources.Load<Sprite>(filePath+"no-image");
		Sprite locked = Resources.Load<Sprite>(filePath+"locked-element");

		//adds the locked image to the sprite dictionary
		if (!GlobalVars.ELEMENT_SPRITES.ContainsKey("locked")) {
			GlobalVars.ELEMENT_SPRITES.Add("locked", locked);
		}

		updatePercentUnlocked();
		if (!GlobalVars.SPRITES_LOADED) {
			//creates all the element spawners
			for (int i = 0; i < GlobalVars.ELEMENTS.Count; i++) {
				//fetches the element's name
				string name = GlobalVars.ELEMENTS[i].getName();

				//loads the sprite
				Sprite sprite = Resources.Load<Sprite>(filePath+name);

				//adds the elemnts sprite to the dictionary
				if (sprite == null) {
					GlobalVars.ELEMENT_SPRITES.Add (name, noImage);
				} else {
					GlobalVars.ELEMENT_SPRITES.Add (name, sprite);
				}
			}
		}

		//loads all the tierbutton scripts into an array
		tierButtonScript = new TierButtonDisplay [TierButtonDisplay.AllTierButtons.Count];
		for (int i = 0; i < TierButtonDisplay.AllTierButtons.Count; i++) {
			tierButtonScript[i] = TierButtonDisplay.AllTierButtons[i];
		}

		//loads all the element controllers into an array
		elementPanelControllers = new SpawnerControl[elementPanels.Length];
		for (int i = 0; i < elementPanelControllers.Length; i++) {
			elementPanelControllers[i] = elementPanels[i].GetComponent<SpawnerControl>();
		}

		//loads the elements from the first tier
		loadTier (0, true);

		//flag so the sprites are not read again while the game is open
		GlobalVars.SPRITES_LOADED = true;

		//adds the discovery event to the unlock new elment function
		CraftingControl.OnElementDiscovered += unlockNewElement;

		//event reference for the outro sequence for unlocking life
		Element.OnLifeUnlocked += playUnlockLifeVideo;

		//subscribes to the event to switch tiers
		TierButtonDisplay.OnLoadTier += loadTier;

		//runs any tutorials that are callable
		CheckForTutorialEvents();
	}

	//unsubscribes the events when the object is destroyed
	void OnDestroy () {
		CraftingControl.OnElementDiscovered -= unlockNewElement;
		Element.OnLifeUnlocked -= playUnlockLifeVideo;
		TierButtonDisplay.OnLoadTier -= loadTier;
	}

	//sets the UI for the currently loaded tier	
	public void loadTier (int tier, bool forceLoad, int startingElement) {
		if (!firstCall && OnButtonPress != null) { //so the button click doesn't play on load
			//button sound effect
			OnButtonPress();
		} else {
			firstCall = false;
		}

		//loads the tier if it's unlocked and not the current one
		if ((tier != currentlySelectedTierNumber || forceLoad) && GlobalVars.TIER_UNLOCKED[tier]) {

			//calls the on load tier event
			if (OnLoadTier != null) {
				OnLoadTier(tier);
			}

			//sets the right button active
			toggleTierButtons(tier);

			//used to index through the elements and panels
			int count = 0;
			int numberUnlocked = 0;
			int elementStartIndex = startingElement;

			//toggles the page 2 button on the bottom on and off
			buttonControl.togglePageButton(2, !(GlobalVars.ELEMENTS_BY_TIER[tier].Count <= elementPanels.Length));

			//sets the correct button on the bottom active
			buttonControl.setButtonActive(Mathf.Clamp(startingElement, 0, 1)+1);
			List<Element> elementsToLoad;
			if (tier == 0 || tier == 1) {
				//creates new list 
				elementsToLoad = new List<Element>();
				//copies all elements from tier 1
				foreach (Element element in GlobalVars.ELEMENTS_BY_TIER[0]) {
					elementsToLoad.Add(element);
				}
				//copies all elements from tier 0
				foreach (Element element in GlobalVars.ELEMENTS_BY_TIER[1]) {
					elementsToLoad.Add(element);
				}
			} else {
				//loads in tier of elements if not tier 1 or 2
				elementsToLoad = GlobalVars.ELEMENTS_BY_TIER[tier];
			}

			//indexes through the elements and sets the proper ones
			foreach (Element e in elementsToLoad) {

				//increases the unlocked count which is show on the 
				if (e.isElementUnlocked()) {
					numberUnlocked++;
				}
				//sets the element panels that should be active
				if (count < elementPanels.Length + elementStartIndex && count >= elementStartIndex) {
					elementPanels[count - elementStartIndex].SetActive(true);
					elementPanelControllers[count - elementStartIndex].setElement(e);
				} 
				count++;
			}

			//hides the unused panels
			for (int i = count - elementStartIndex; i < elementPanels.Length; i++) {
				elementPanels[i].SetActive(false);
			}

			//updates the tier number for the script's reference
			currentlySelectedTierNumber = tier;

		}

	}

	//overload method to load without a starting element number
	public void loadTier (int tier, bool forceload) {
		loadTier(tier, forceload, 0);
	}

	//overload method to load tier without force load condition (because UI buttons can only call scripts with one argument or less)
	public void loadTier (int tier) {
		loadTier(tier, false);
	}

	//method to load a new page of elements of the same tier
	public void loadElementPage (int page) {
		loadTier (currentlySelectedTierNumber, true, page * elementPanels.Length);
	}


	//activates and deactives the proper buttons to highlight the selected one
	public void toggleTierButtons (int activeButton) { 

		//offset due to the fact that there are two tiers in one
		int tierOffset = 0;
		int activeOffset = -1;

		//toggles the other tier buttons active and inactive
		for (int i = tierOffset; i < tierButtonScript.Length; i++) {
			tierButtonScript[i].updateTierProgress();
			/*if (!GlobalVars.TIER_UNLOCKED[i]) {
				tierButtonScript[i].makeButtonLocked();
			} else*/
			if (i == activeButton + activeOffset) {
				tierButtonScript[i].makeButtonSelected(true);
			} else if (tierButtonScript[i].isUnlocked) { 
				tierButtonScript[i].makeButtonSelected(false);
			}

			/*else {
				tierButtonScript[i].makeButtonSelected(false);
			}*/
		}


	}

	//refreshes the tier buttons (for when a new element or tier is unlocked
	public void toggleTierButtons () {
		toggleTierButtons(currentlySelectedTierNumber);
	}

	//unlocks a tier button, for when the first element from it is discovered 
	public void unlockTierButton (int tierNumber) {
		tierButtonScript[tierNumber].makeButtonUnlocked();
	}

	//hides the element and locks the panel
	public void lockPanel (int panelIndex) {
		elementPanelControllers[panelIndex].togglePanelLock();
	}
	
	//shows the element and swiches the panel background
	public void unlockPanel (int panelIndex) {
		//elementPanels[panelIndex].GetComponent<Image>().sprite = panelUnlocked;
		for (int i = 0; i < elementPanels[panelIndex].transform.childCount; i++) {
			if (elementPanels[panelIndex].name == "BackgroundLocked") {
				elementPanels[panelIndex].transform.GetChild (i).gameObject.SetActive(false);
			} else {
				elementPanels[panelIndex].transform.GetChild (i).gameObject.SetActive(true);
			}
		}
	}

	//updates the inventory counts on element panels
	public void updatePanelCounts () {
		for (int i = 0; i < elementPanelControllers.Length; i++) {
			if (elementPanels[i] != null && elementPanels[i].activeSelf) {
				elementPanelControllers[i].setInventoryCount();
			}
		}
	}

	//updates the unlock progress display
	public void updatePercentUnlocked () {
		//sets the sprite based o how many elements are unlocked
		float unlockedFraction = ((float)GlobalVars.NUMBER_ELEMENTS_UNLOCKED * (float) (unlockProgressSprites.Length - 1))/(float)GlobalVars.ELEMENTS.Count;
		unlockProgress.sprite = unlockProgressSprites[(int) unlockedFraction];

		//sets the unlock count text
		string unlockCount = GlobalVars.NUMBER_ELEMENTS_UNLOCKED.ToString(); 

		//adds zeros to the beginning of the count
		while (unlockCount.Length > 3) {
			unlockCount = "0" + unlockCount;
		}

		//sets the text
		numberUnlocked.text = unlockCount;

	}

	public void lockAllTierButtons () {
		for (int i = 1; i < tierButtonScript.Length; i++) {
			tierButtonScript[i].makeButtonLocked();
			GlobalVars.TIER_UNLOCKED[i] = false;
		}
		updatePercentUnlocked();
	}

	//unlocks a new element when it is crafted	
	public void unlockNewElement (string newElementName) {
		/*
		 * only necsessary for tier one and two because they share the same page 
		 * (for other tiers, you won't see the elment unlocked because every element is created by ones from lower tiers
		*/
		Element newElement = GlobalVars.ELEMENTS_BY_NAME[newElementName];
		if (newElement.getTier() == 2 && (currentlySelectedTierNumber == 0 || currentlySelectedTierNumber == 1)) { 
			//finds the indx of the elment (it will always be in tier 2)
			int elementIndex = System.Array.IndexOf(GlobalVars.ELEMENTS_BY_TIER[1].ToArray(), newElement);
			elementPanelControllers[GlobalVars.ELEMENTS_BY_TIER[0].Count + elementIndex].setElement(newElement);
		}
	}

	public void callHintPanel(){
		hintPanel.SetActive (true);
		hintPanel.GetComponent<PurchaseHint> ().elementHint = activeElement;
		int theCost = 10 * GlobalVars.ELEMENTS_BY_NAME [activeElement.ToLower ()].getTier ();
		bool wasPreviouslyBought = PlayerPrefs.GetInt (activeElement.ToLower () + "Hint") == 1;
		hintPanel.transform.FindChild("AlreadyPurchased").gameObject.SetActive(wasPreviouslyBought);
		hintPanel.transform.FindChild("NotYetPurchased").gameObject.SetActive(!wasPreviouslyBought);

		hintPanel.GetComponent<PurchaseHint> ().myCost1 = 
			hintPanel.GetComponent<PurchaseHint> ().myCost2 = 
			hintPanel.GetComponent<PurchaseHint> ().myCost3 = 
			hintPanel.GetComponent<PurchaseHint> ().myCost4 = theCost;

		for (int i = 1; i<=4; i++) {
			hintPanel.transform.FindChild("NotYetPurchased/PurchaseCost/cost"+i.ToString()).GetComponent<Text>().text = theCost.ToString();
			hintPanel.transform.FindChild("NotYetPurchased/PurchaseCost/myAmount"+i.ToString()).GetComponent<Text>().text = PlayerPrefs.GetInt (hintPanel.GetComponent<PurchaseHint> ().getCostElemType(i)).ToString();
		}
		hintPanel.transform.FindChild ("AlreadyPurchased/Name").GetComponent<Text> ().text = activeElement;
		hintPanel.transform.position = activePosition;
	}


	public void playUnlockLifeVideo () {
		#if UNITY_ANDROID || UNITY_IOS
			Handheld.PlayFullScreenMovie ("outro.mp4", Color.black, FullScreenMovieControlMode.CancelOnInput);
		#endif
	}

	
	public bool ReadyToGather () {
		return buttonControl.PollGatheringDropZones() == GlobalVars.NUMBER_OF_LANES;
	}
	
#region TUTORIAL

	//calls any applicable tutorial events
	private void CheckForTutorialEvents () {
		//checks whether the conditions for each tutorial have been met and runs them if so (tutorials only play once per device, unless the player resets all progress)
		if (OnCallTutorialEvent != null) {

			//the gathering tutorial
			if (!Utility.PlayerPrefIntToBool(GlobalVars.ELEMENTS_DRAGGED_TUTORIAL_KEY)) {
				OnCallTutorialEvent(TutorialType.Gathering);

			} 

			//the crafting tutorial
			else if (!Utility.PlayerPrefIntToBool(GlobalVars.CRAFTING_TUTORIAL_KEY) && 
			           Utility.SufficientElementsToPurchase(2, new KeyValuePair<string, int>("fire", 1),
			                                     new KeyValuePair<string, int>("water", 1), 
			                                     new KeyValuePair<string, int>("earth", 1), 
			                                     new KeyValuePair<string, int>("air", 1)))  {
				OnCallTutorialEvent(TutorialType.Crafting);

			} 

			//the buying a hint tutorial
			else if (!Utility.PlayerPrefIntToBool(GlobalVars.BUY_HINT_TUTORIAL_KEY) &&
			           Utility.SufficientElementsToPurchase(tutorialHint.GetCosts())) {
				OnCallTutorialEvent(TutorialType.BuyHint);
			} 

			//the upgrading a powerup tutorial
			else if (!Utility.PlayerPrefIntToBool(GlobalVars.UPGRADE_POWERUP_TUTORIAL_KEY) &&
			         Utility.SufficientElementsToPurchase(BuyUpgrade.GetBaseCosts()) &&
				       Element.AllTierElementsUnlocked(0) && 
			           Element.AllTierElementsUnlocked(1)) {
				OnCallTutorialEvent(TutorialType.UpgradePowerup);
			} 

			//tier switch
			else if (!Utility.PlayerPrefIntToBool(GlobalVars.TIER_SWITCH_TUTORIAL_KEY) &&
			           GlobalVars.TIER_UNLOCKED[2]) {

				CallTierSwitchTutorial();
			}	
		} else {
#if DEBUG
			Debug.LogError("Tbe event is null");
#endif
		}
	}

	//sets the PurchaseHint script
	public void SetTutorialPurchaseHint (PurchaseHint hint) {
		tutorialHint = hint;
	}

	public void CallTierSwitchTutorial () {
		OnCallTutorialEvent(TutorialType.TierSwitch);
	}
#endregion
}
