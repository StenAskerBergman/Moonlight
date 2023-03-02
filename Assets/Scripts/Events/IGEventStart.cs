using UnityEngine;
using System;

public class IGEventStart : MonoBehaviour {
    public event EventHandler<OnSpacePressedEventArgs> OnSpacePressed;
    public class OnSpacePressedEventArgs : EventArgs{
        public int spaceCount;
    }
    private int spaceCounts;
    //public event EventHandler LocalEvent;
    //public event EventHandler GlobalEvent;

    private void Start(){
        //LocalEvent?.Invoke(this, EventArgs.Empty);
        //GlobalEvent?.Invoke(this, EventArgs.Empty);
    }

    private void Update(){
        if (Input.GetKeyDown(KeyCode.Space)){
            // Space Pressed!
            spaceCounts++;

            OnSpacePressed?.Invoke(this, new OnSpacePressedEventArgs {spaceCount = spaceCounts});   
            
            //if (OnSpacePressed != null) OnSpacePressed(this, EventArgs.Empty);

        }
        
    }

}
