# Customizing Behavior
There are settings and attributes unique to SyncTypes which allow various ways of customizing your SyncType.

## SyncTypeSettings
SyncTypeSettings can be initialized with any SyncType to define the default settings of your SyncType.

```csharp
//Custom settings are optional.
//This is an example of declaring a SyncVar without custom settings.
private readonly SyncVar<int> _myInt = new();

//Each SyncType has a different constructor to take settings.
//Here is an example for SyncVars. This will demonstrate how to use
//the unreliable channel for SyncVars, and send the value upon any change.
//There are many different ways to create SyncTypeSettings; you can even
//make a const settings and initialize with that!
private readonly SyncVar<int> _myInt = new(new SyncTypeSettings(0f, Channel.Unreliable));
```
Settings can also be changed at runtime. This can be very useful to change behavior based on your game mechanics and needs, or to even slow down send rate as your player count grows.

```csharp
//This example shows in Awake but this code
//can be used literally anywhere.
private void Awake()
{
//You can change all settings at once.
_myInt.UpdateSettings(new SyncTypeSettings(....);

        //Or update only specific things, such as SendRate.
    //Change send rate to once per second.
    _myInt.UpdateSendRate(1f);
}
```

## Showing In The Inspector
SyncTypes can also be shown in the inspector.

You must first make sure your type is marked as serializable if a container; this is a Unity requirement.

```csharp
//SyncTypes can be virtually any data type. This example
//shows a container to demonstrate the serializable attribute.
[System.Serializable]
public struct MyType { }
```
Next the SyncType must not use the 'readonly' indicator. We require the readonly indicator by default to emphasis you should not initialize your SyncType at runtime.

Below is an example of what NOT to do.

```csharp
private SyncVar<int> _myInt = new();

private void Awake()
{
//This would result in errors at runtime.

    //Do not make a SyncType into a new instance
    _myInt = new();
    //Do not set a SyncType to another instance.
    _myInt = _someOtherDeclaredSyncVar.
}
```
The code above will actually prevent compilation in the editor as our code generators will detect you did not include the readonly indicator. To remove the readonly indicator you must also add the AllowMutableSyncType above your SyncType.

```csharp
//This will work and show your SyncType in the inspector!
[AllowMutableSyncType]
[SerializeField] //Be sure to serializeField if not public.
private SyncVar<int> _myInt = new();
```

# SyncVar
SyncVars are the most simple way to automatically synchronize a single variable over the network.

SyncVars are used to synchronize a single field. Your field can be virtually anything: a value type, struct, or class. To utilize a SyncVar you must implement your type as a SyncVar class

```csharp

public class YourClass : NetworkBehaviour
{
private readonly SyncVar<float> _health = new SyncVar<float>();
}
/* Any time _health is changed on the server the
* new value will be sent to clients. */
```
  SyncTypes can also be customized with additional options by using the UpdateSettings method within your declared SyncTypes, or using the initializer of your SyncType. These options include being notified when the value changes, changing how often the SyncType will synchronize, and more. You can view a full list of SyncVar properties which may be changed by viewing the API.

Below is a demonstration on sending SyncTypes at a longer interval of at most every 1f, and being notified of when the value changes.

```csharp

private readonly SyncVar<float> _health = new SyncVar<float>(new SyncTypeSettings(1f);

private void Awake()
{
_health.OnChange += on_health;
}

//This is called when _health changes, for server and clients.
private void on_health(float prev, float next, bool asServer)
{
/* Each callback for SyncVars must contain a parameter
* for the previous value, the next value, and asServer.
* The previous value will contain the value before the
* change, while next contains the value after the change.
* By the time the callback occurs the next value had
* already been set to the field, eg: _health.
* asServer indicates if the callback is occurring on the
* server or on the client. Sometimes you may want to run
* logic only on the server, or client. The asServer
* allows you to make this distinction. */
}
```
Another common request is achieving client-side SyncVar values. This may be achieved by using a ServerRpc.

When modifying SyncVars using client-side, a RPC is sent with every set. If your SyncVar value will change frequently consider limiting how often you set the value client-side to reduce bandwidth usage.

In a future release we are planning to make all SyncTypes have client-authoritative properties where this work-around will not be needed.

```csharp

//A typical server-side SyncVar.
public readonly SyncVar<string> Name = new SyncVar<string>();
//Create a ServerRpc to allow owner to update the value on the server.
[ServerRpc] private void SetName(string value) => Name.Value = value;
```
When using client-side SyncVars you may want to consider ExcludeOwner in the SyncVar ReadPermissions to prevent owners from receiving their own updates. In addition, using WritePermission.ClientUnsynchronized will allow the client to set the value locally. Lastly, RunLocally as true in the ServerRpc will execute the RPC code on both the sender(client) and the server.

Using ExcludeOwner as the SyncVar ReadPermissions, and ClientUnsynchronized in the WritePermissions. . In the example below we also set RunLocally to true for the ServerRpc so that the calling client also sets the value locally.

```csharp
//Attributes shown in previous examples can stack but they were removed
//here for simplicity.
private readonly SyncVar<string> Name = new SyncVar<string>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));
//Create a ServerRpc to allow owner to update the value on the server.
[ServerRpc(RunLocally = true)] private void SetName(string value) => Name.Value = value;
```

# SyncList
SyncList is an easy way to keep a List collection automatically synchronized over the network.

Using a SyncList is done the same as with a normal List.

Network callbacks on SyncList do have a little more information than SyncVars. Other non-SyncVar SyncTypes also have their own unique callbacks. The example below demonstrates a SyncList callback.

```csharp

private readonly SyncList<int> _myCollection = new();

private void Awake()
{
/* Listening to SyncList callbacks are a
* little different from SyncVars. */
_myCollection.OnChange += _myCollection_OnChange;
}
private void Update()
{
//You can modify a synclist as you would any other list.
_myCollection.Add(10);
_myCollection.RemoveAt(0);
//ect.
}

/* Like SyncVars the callback offers an asServer option
* to indicate if the callback is occurring on the server
* or the client. As SyncVars do, changes have already been
* made to the collection before the callback occurs. */
  private void _myCollection_OnChange(SyncListOperation op, int index,
  int oldItem, int newItem, bool asServer)
  {
  switch (op)
  {
  /* An object was added to the list. Index
  * will be where it was added, which will be the end
  * of the list, while newItem is the value added. */
  case SyncListOperation.Add:
  break;
  /* An object was removed from the list. Index
  * is from where the object was removed. oldItem
  * will contain the removed item. */
  case SyncListOperation.RemoveAt:
  break;
  /* An object was inserted into the list. Index
  * is where the obejct was inserted. newItem
  * contains the item inserted. */
  case SyncListOperation.Insert:
  break;
  /* An object replaced another. Index
  * is where the object was replaced. oldItem
  * is the item that was replaced, while
  * newItem is the item which now has it's place. */
  case SyncListOperation.Set:
  break;
  /* All objects have been cleared. Index, oldValue,
  * and newValue are default. */
  case SyncListOperation.Clear:
  break;
  /* When complete calls all changes have been
  * made to the collection. You may use this
  * to refresh information in relation to
  * the list changes, rather than doing so
  * after every entry change. Like Clear
  * Index, oldItem, and newItem are all default. */
  case SyncListOperation.Complete:
  break;
  }
}
```
  If you are using this SyncType with a container, such as a class, and want to modify values within that container, you must set the value dirty. See the example below.

```csharp

private class MyClass
{
public string PlayerName;
public int Level;
}

private readonly SyncList<MyClass> _players = new SyncList<MyClass>();

//Call dirty on an index after modifying an entries field to force a synchronize.
[Server]
private void ModifyPlayer()
{
_players[0].Level = 10;
//Dirty the 0 index.
_players.Dirty(0);
}
```
Structures cannot have their values modified when they reside within a collection. You must instead create a local variable for the collection index you wish to modify, change values on the local copy, then set the local copy back into the collection

```csharp

/* . */
[System.Serializable]
private struct MyStruct
{
    public string PlayerName;
    public int Level;
}

private readonly SyncList<MyStruct> _players = new();

[Server]
private void ModifyPlayer()
{
    MyStruct ms = _players[0];
    ms.Level = 10;
    _players[0] = ms;
}
```

# SyncHashSet Usage in Unity

## Overview

This guide provides an example of using SyncHashSet in Unity, handling changes, and ensuring proper synchronization.

Code Example
```csharp
private void Awake() _myCollection.OnChange += _myCollection_OnChange;

// You can modify a SyncHashSet as you would any other HashSet.
private void FixedUpdate() _myCollection.Add(Time.frameCount);

/* Like SyncVars, the callback offers an asServer option
* to indicate if the callback is occurring on the server
* or the client. As SyncVars do, changes have already been
* made to the collection before the callback occurs. */
private void _myCollection_OnChange(SyncHashSetOperation op, int item, bool asServer)
{
  switch (op)
  {
  /* An object was added to the hashset. Item is
  * the added object. */
  case SyncHashSetOperation.Add:
  break;

       /* An object has been removed from the hashset. Item
        * is the removed object. */
       case SyncHashSetOperation.Remove:
           break;
       
       /* The hashset has been cleared.
        * Item will be default. */
       case SyncHashSetOperation.Clear:
           break;
       
       /* An entry in the hashset has been updated.
        * When this occurs the item is removed
        * and added. Item will be the new value.
        * Item will likely need a custom comparer
        * for this to function properly. */
       case SyncHashSetOperation.Update:
           break;
       
       /* When complete calls all changes have been
       * made to the collection. You may use this
       * to refresh information in relation to
       * the changes, rather than doing so
       * after every entry change. All values are
       * default for this operation. */
       case SyncHashSetOperation.Complete:
           break;
  }
}
```

## Important Note

If you are using this SyncType with a container, such as a class or structure, and want to modify values within that container, you must set the value dirty. See the example below.

## Using SyncHashSet with Containers
```csharp
[System.Serializable]
private struct MyContainer
{
    public string PlayerName;
    public int Level;
}

private readonly SyncHashSet<MyContainer> _containers = new();
private MyContainer _containerReference = new();

private void Awake() _containers.OnChange += _containers_OnChange;
```


This ensures proper synchronization of structured data in your networked Unity application.

# SyncDictionary
SyncDictionary is an easy way to keep a Dictionary collection automatically synchronized over the network.

SyncDictionary supports all the functionality a normal dictionary would, just as SyncList supports List abilities.

Callbacks for SyncDictionary are similar to SyncList. And like other synchronization types changes are set immediately before the callback occurs.

```csharp
private readonly SyncDictionary<NetworkConnection, string> _playerNames = new();

private void Awake()
{
_playerNames.OnChange += _playerNames_OnChange;
}

//SyncDictionaries also include the asServer parameter.
private void _playerNames_OnChange(SyncDictionaryOperation op,
NetworkConnection key, string value, bool asServer)
{
/* Key will be provided for
* Add, Remove, and Set. */     
switch (op)
{
//Adds key with value.
case SyncDictionaryOperation.Add:
break;
//Removes key.
case SyncDictionaryOperation.Remove:
break;
//Sets key to a new value.
case SyncDictionaryOperation.Set:
break;
//Clears the dictionary.
case SyncDictionaryOperation.Clear:
break;
//Like SyncList, indicates all operations are complete.
case SyncDictionaryOperation.Complete:
break;
}
}
```
If you are using this SyncType with a container, such as a class or structure, and want to modify values within that container, you must set the value dirty. See the example below.

```csharp
[System.Serializable]
private struct MyContainer
{
public string PlayerName;
public int Level;
}

private readonly SyncDictionary<int, MyContainer> _containers = new();

private void Awake()
{
MyContainer mc = new MyContainer{Level = 5};
_containers[2] = mc;
}

[Server]
private void ModifyContainer()
{
MyContainer mc = _containers[2];
//This will change the value locally but it will not synchronize to clients.
mc.Level = 10;
//You may re-apply the value to the dictionary.
_containers[2] = mc;
//Or set dirty on the value or key. Using the key is often more performant.
_containers.Dirty(2);
}
```

# SyncTimer
SyncTimer provides an efficient way to synchronize a timer between server and clients.

Unlike SyncVars, a SyncTimer only updates state changes such as start or stopping, rather than each individual delta change.

SyncTimer is a Custom SyncType, and is declared like any other SyncType.

private readonly SyncTimer _timeRemaining = new SyncTimer();
Making changes to the timer is very simple, and like other SyncTypes must be done on the server.

```csharp
//All of the actions below are automatically synchronized.

/* Starts the timer with 5 seconds on it. The optional boolean argument will also send a stop event before starting a new timer, only if the previous timer is still running. EG: if a timer was started with 5 seconds and you start a new timer with 2 seconds remaining a StopTimer will be sent with the remaining time of 2 seconds before the timer is started again at 5 seconds. */
  _timeRemaining.StartTimer(5f, true);
  /* Pauses the timer and optionally sends the current timer value as it is on the server. */
  _timeRemaining.PauseTimer(false);
  //Unpauses the current timer.
  _timeRemaining.UnpauseTimer();
  /* Stops the timer early and optionally sends the current timer value as it is on the server. */
  _timeRemaining.StopTimer(false);
  Updating and reading the timer value is much like you would a normal float value.

private void Update()
{
    /* Timers must be updated both on the server and clients. This only needs to be done on either/or if clientHost. The timer may be updated in any method. */
    _timeRemaining.Update();

    /* Access the current time remaining. This is how you can utilize the current timer value. When a timer is complete the remaining value will be 0f. */
    Debug.Log(_timeRemaining.Remaining);
    /* You may also see if the timer is paused before accessing time remaining. */
    if (!_timeRemaining.Paused)
        Debug.Log(_timeRemaining.Remaining);
}
```
Like other SyncTypes, you can subscribe to change events for the SyncTimer.
```csharp
private void Awake() _timeRemaining.OnChange += _timeRemaining_OnChange;

private void OnDestroy() _timeRemaining.OnChange -= _timeRemaining_OnChange;

private void _timeRemaining_OnChange(SyncTimerOperation op, float prev, float next, bool asServer)
{
/* Like all SyncType callbacks, asServer is true if the callback is occuring on the server side, false if on the client side. */

    //Operations can be used to be notified of changes to the timer.

    //Timer has been started with initial values.
    if (op == SyncTimerOperation.Start)
        Debug.Log($"The timer was started with {next} seconds.");
    //Timer has been paused.
    else if (op == SyncTimerOperation.Pause)
        Debug.Log($"The timer was paused.");
    //Timer has been paused and latest server values were sent. 
    else if (op == SyncTimerOperation.PauseUpdated)
        Debug.Log($"The timer was paused and remaining time has been updated to {next} seconds.");
    //Timer was unpaused.
    else if (op == SyncTimerOperation.Unpause)
        Debug.Log($"The timer was unpaused.");
    //Timer has been manually stopped.
    else if (op == SyncTimerOperation.Stop)
        Debug.Log($"The timer has been stopped and is no longer running.");
    /* Timer has been manually stopped. When StopUpdated is called Previous will contain the remaining time prior to being stopped as it is locally. Next will contain the remaining time prior to being stopped as it was on the server. These values often align but the information is provided for your potential needs.  When the server starts a new timer while one is already active, and chooses to also send a stop update using the StartTimer(float,bool) option, a StopUpdated is also sent to know previous timer values before starting a new timer. */
    else if (op == SyncTimerOperation.StopUpdated)
        Debug.Log($"The timer has been stopped and is no longer running. The timer was stopped at value {next} before stopping, and the previous value was {prev}");
    //A timer has reached 0f.
    else if (op == SyncTimerOperation.Finished)
        Debug.Log($"The timer has completed!");
    //Complete occurs after all change events are processed.
    else if (op == SyncTimerOperation.Complete)
        Debug.Log("All timer callbacks have completed for this tick.");
    }
```

# SyncStopwatch
SyncStopwatch provides an efficient way to synchronize a stopwatch between server and clients.

Like SyncTimer, SyncStopwatch only updates state changes such as start or stopping, rather than each individual delta change.

```csharp
private readonly SyncStopwatch _timePassed = new()
```
Making changes to the timer is very simple, and like other SyncTypes must be done on the server.

```csharp
//All of the actions below are automatically synchronized.

/* Starts the stopwatch while optionally sending a stop
* message first if stopwatch was already running.
* If the stopwatch was previously running the time passed
* would be reset. */
  /* This would invoke callbacks indicating a stopwatch
* had stopped, then started again. */
  _timePassed.StartStopwatch(true);
  /* Pauses the stopwatch and optionally sends the current
* timer value as it is on the server. */
  _timePassed.PauseStopwatch(false);
  //Unpauses the current stopwatch.
  _timePassed.UnpauseStopwatch();
  /* Ends the Stopwatch while optionally sends the
* current value to clients, as it was during the stop. */
  _timePassed.StopStopwatch(false);
  ```
  Updating and reading the timer value is much like you would a normal float value.


```csharp
private void Update()
{
/* Like SyncTimer, SyncStopwatch must be updated
* with a delta on both server and client. Do not
* update the delta twice if clientHost. You can update
* the delta anywhere in your code. */
_timePassed.Update(Time.deltaTime);

    //Access the current time passed.
    Debug.Log(_timePassed.Elapsed);
    /* You may also see if the stopwatch is paused before
     * accessing elapsed. */
    if (!_timePassedPaused)
        Debug.Log(_timePassed.Elapsed);
}
Like other SyncTypes, you can subscribe to change events for the SyncStopwatch.
```

```csharp
private void Awake()
{
_timePassed.OnChange += _timePassed_OnChange;
}

private void OnDestroy()
{
_timePassed.OnChange -= _timePassed_OnChange;
}

private void _timePassed_OnChange(SyncStopwatchOperation op, float prev, bool asServer)
{
/* Like all SyncType callbacks, asServer is true if the callback
* is occuring on the server side, false if on the client side. */

    //Operations can be used to be notified of changes to the timer.
    //This is much like other SyncTypes.
    //Here is an example of performing logic only if starting or stopping.
    if (op == SyncStopwatchOperation.Start || op == SyncStopwatchOperation.Stop)
    {
        //Do logic.
    }
    
    /* prev, our float, indicates the value of the stopwatch prior to
     * the operation. Remember that you can get current value using
     * _timePassed.Elapsed */
}
```

# Custom SyncType
With a customized SynType you can decide how and what data to synchronize, and make optimizations as you see fit.

For example: if you have a data container with many variables you probably don't want to send the entire container when you change it, as a SyncVar would. By making a custom SyncType you can customize the behavior entirely; this is how other SyncType work.

```csharp
/* If one of these values change you
* probably don't want to send the
* entire container. A custom SyncType
* is perfect for only sending what is changed. */
  [System.Serializable]
  public struct MyContainer
  {
  public int LeftArmHealth;
  public int RightArmHealth;
  public int LeftLegHealth;
  public int RightLeftHealth;            
  }
  ```
  Custom SyncTypes follow the same rules as other SyncTypes. Internally other SyncTypes inherit from SyncBase, and your type must as well. In addition, you must implement the ICustomSync interface.

```csharp
public class SyncMyContainer : SyncBase, ICustomSync
{
/* If you intend to serialize your type
* as a whole at any point in your custom
* SyncType and would like the automatic
* serializers to include it then use
* GetSerializedType() to return the type.
* In this case, the type is MyContainer.
* If you do not need a serializer generated
* you may return null. */
public object GetSerializedType() => typeof(MyContainer);
}

public class YourClass
{
private readonly SyncMyContainer _myContainer = new();
}
```
Given how flexible a custom SyncType may be there is not a one-size fits all example. You may view several custom examples within your FishNet import under FishNet/Example/All/CustomSyncType.

# Broadcast
Broadcasts allow you to send messages to one or more objects without them requiring a NetworkObject component.
This could be useful for communicating between objects which are not necessarily networked, such as a chat system.

Like Remote Procedure Calls, broadcasts may be sent reliably or unreliably. 
Data using broadcasts can be sent from either from the client to the server, or server to client(s). 
Serializers are automatically generated for Broadcasts as well.

Broadcasts must be structures, and implement IBroadcast. 
Below demonstrates what values a chat broadcast may possibly contain.

```csharp
public struct ChatBroadcast : IBroadcast
{
public string Username;
public string Message;
public Color FontColor;
}
```
Since broadcasts are not linked to objects they must be sent using the ServerManager, or ClientManager. When sending to the server you will send using ClientManager, and when sending to clients, use ServerManager.

Here is an example of sending a chat message from a client to the server.

```csharp
public void OnKeyDown_Enter(string text)
{
//Client won't send their username, server will already know it.
ChatBroadcast msg = new ChatBroadcast()
{
Message = text,
FontColor = Color.white
};

    InstanceFinder.ClientManager.Broadcast(msg);
}
```
Sending from the server to client(s) is done very much the same but you are presented with more options. For a complete list of options I encourage you to view the API. Here is an example of sending a broadcast to all clients which have visibility of a specific client. This establishes the idea that clientA sends a chat message to the server, and the server relays it to other clients which can see clientA. In this example clientA would also get the broadcast.

```csharp
//When receiving broadcast on the server which connection
//sent the broadcast will always be available.
public void OnChatBroadcast(NetworkConnection conn, ChatBroadcast msg, Channel channel)
{
//For the sake of simplicity we are using observers
//on conn's first object.
NetworkObject nob = conn.FirstObject;

    //The FirstObject can be null if the client
    //does not have any objects spawned.
    if (nob == null)
        return;
        
    //Populate the username field in the received msg.
    //Let us assume GetClientUsername actually does something.
    msg.Username = GetClientUsername(conn);
        
    //If you were to view the available Broadcast methods
    //you will find we are using the one with this signature...
    //NetworkObject nob, T message, bool requireAuthenticated = true, Channel channel = Channel.Reliable)
    //
    //This will send the message to all Observers on nob,
    //and require those observers to be authenticated with the server.
    InstanceFinder.ServerManager.Broadcast(nob, msg, true);
}
Given broadcasts are not automatically received on the object they are sent from you must specify what scripts, or objects can receive a broadcast. As mentioned previously, this allows you to receive broadcast on non-networked objects, but also enables you to receive the same broadcast on multiple objects.

While our example only utilizes one object, this feature could be useful for changing a large number of conditions in your game at once, such as turning off or on lights without having to make them each a networked object.

Listening for a broadcast is much like using events. Below demonstrates how the client will listen for data from the server.
```
```csharp
private void OnEnable()
{
//Begins listening for any ChatBroadcast from the server.
//When one is received the OnChatBroadcast method will be
//called with the broadcast data.
InstanceFinder.ClientManager.RegisterBroadcast<ChatBroadcast>(OnChatBroadcast);
}

//When receiving on clients broadcast callbacks will only have
//the message. In a future release they will also include the
//channel they came in on.
private void OnChatBroadcast(ChatBroadcast msg, Channel channel)
{
//Pretend to print to a chat window.
Chat.Print(msg.Username, msg.Message, msg.FontColor);
}

private void OnDisable()
{
//Like with events it is VERY important to unregister broadcasts
//When the object is being destroyed(in this case disabled), or when
//you no longer wish to receive the broadcasts on that object.
InstanceFinder.ClientManager.UnregisterBroadcast<ChatBroadcast>(OnChatBroadcast);
}
```
As a reminder, a receiving method on the server was demonstrated above. The method signature looked like this.

```csharp
public void OnChatBroadcast(NetworkConnection conn, ChatBroadcast msg)
```
With that in mind, let's see how the server can listen for broadcasts from clients.

Registering for the server is exactly the same as for clients.
Note there is an optional parameter not shown, requireAuthentication.
The value of requireAuthentication is default to true.
Should a client send this broadcast without being authenticated the server would kick them.

```csharp
private void OnEnable() InstanceFinder.ServerManager.RegisterBroadcast<ChatBroadcast>(OnChatBroadcast);

//There are no differences in unregistering.
private void OnDisable() InstanceFinder.ServerManager.UnregisterBroadcast<ChatBroadcast>(OnChatBrodcast);
```
If you would like to view a working example of using Broadcast view the PasswordAuthentictor.cs file within the examples folder.

# Observers
An observer is a client which can see an object, and use communications for the object. You may control which clients can observe an object by using the NetworkObserver and/or ObserverManager components.

If a client is not an observer of an object then the object will not active, and the client will not receive network messages or callbacks for that object. Should the object be a scene object then it will remain disabled on the client until they become an observer of it. If the object is instantiated then the client will simply not instantiate the object until after becoming an observer.

The observer system is designed to work out of the box for new developers. When it comes time to customize how clients observe objects, the observer system additionally offers a large amount of flexibility, keeping in mind there are many condition types, and that you may also create your own.

Fish-Networking comes with a NetworkManager prefab which contains the recommended minimum components to begin working on a new project. Within that prefab is the ObserverManager with an included Scene Condition. If you have not familiarized yourself with the ObserverManager and condition types please do so now using the links above.

A common problem new developers encounter is scene objects not being enabled for clients. This occurs when the client is not considered part of the scene where the object resides, and the scene condition is preventing that object from spawning for the client. The NetworkManager prefab contains a PlayerSpawner script which adds the player to the current scene, which would make the clients an observer for objects in that scene; this also requires a player object to be spawned. Should you have made your own NetworkManager object or removed the PlayerSpawner script you will also need to add the client to the scene you wish the client to be an observer of.

When encountering such an issue you may of course also remove the ObserverManager or scene condition from the ObserverManager, but this is not recommended as objects in other scenes will attempt to spawn for clients which do not occupy such scenes. Alternatively, you may add the client to the scene where the objects reside; there's a variety of ways to accomplish this.

Under the assumption you removed the PlayerSpawner and/or are not using SceneManager.AddOwnerToDefaultScene, then you must load the client into the scene using the SceneManager. Clients are only considered networked into scenes when those scenes are loaded using the SceneManager. Clients may become part of a scene by loading a scene globally, or loading a scene for a specific client(connection). See the SceneManager section for more information on how to manage networked client scenes as well understand the difference between global and connection scenes.

## Modifying Conditions
Several conditions may be modified at run-time. What can be modified for each condition may vary. I encourage you to view the API to see what each condition exposes.

To change properties on a condition you must access the condition through the NetworkObserver component.

```csharp
//Below is an example of modifying the distance requirement
//on a DistanceCondition. This line of code can be called from
//any NetworkBehaviour. You may also use nbReference.NetworkObserver...
base.NetworkObserver.GetObserverCondition<DistanceCondition>().MaximumDistance = 10f;
```
All conditions can be enabled or disabled. When a condition is disabled it's requirements are ignored, as if the condition does not exist. This can be useful for temporarily disabling condition requirements on objects.

```csharp
//The OwnerOnlyCondition is returned and disabled.
//This allows all players to see the object, rather than just the owner.
ObserverCondition c = base.NetworkObject.NetworkObserver.GetObserverCondition<OwnerOnlyCondition>();
c.SetIsEnabled(false);
//Even though we are returning ObserverCondition type, it could be casted to
//OwnerOnlyCondition.
```

## Custom Conditions
Sometimes you may have unique requirements for an observer condition. When this is the case you can easily create your own ObserverCondition. The code below comments on how to create your own condition.

```csharp

//The example below does not have many practical uses
//but it shows the bare minimum needed to create a custom condition.
//This condition makes an object only visible if the connections
//ClientId mathes the serialized value, _id.

//Make a new class which inherits from ObserverCondition.
//ObserverCondition is a scriptable object, so also create an asset
//menu to create a new scriptable object of your condition.
[CreateAssetMenu(menuName = "FishNet/Observers/ClientId Condition", fileName = "New ClientId Condition")]
public class ClientIdCondition : ObserverCondition
{
/// <summary>
/// ClientId a connection must be to pass the condition.
/// </summary>
[Tooltip("ClientId a connection must be to pass the condition.")]
[SerializeField]
private int _id = 0;

    private void Awake()
    {
        //Awake can be optionally used to initialize values based on serialized
        //data. The source file of DistanceCondition is a good example
        //of where Awake may be used.
    }
    
    /// <summary>
    /// Returns if the object which this condition resides should be visible to connection.
    /// </summary>
    /// <param name="connection">Connection which the condition is being checked for.</param>
    /// <param name="currentlyAdded">True if the connection currently has visibility of this object.</param>
    /// <param name="notProcessed">True if the condition was not processed. This can be used to skip processing for performance. While output as true this condition result assumes the previous ConditionMet value.</param>
    public override bool ConditionMet(NetworkConnection connection, bool currentlyAdded, out bool notProcessed)
    {
        notProcessed = false;

        //When true is returned it means the connection meets
        //the condition requirements. When false, the
        //connection does not and will not see the object.

        //Will return true if connection Id matches _id.
        return (connection.ClientId == _id);
    }

    /// <summary>
    /// Type of condition this is. Certain types are handled different, such as Timed which are checked for changes at timed intervals.
    /// </summary>
    /// <returns></returns>
    /* Since clientId does not change a normal condition type will work.
    * See API on ObserverConditionType for more information on what each
    * type does. */
    public override ObserverConditionType GetConditionType() => ObserverConditionType.Normal;
}
```
You can get an idea of the flexibility of a condition by exploring the source of other premade conditions.

# Automatic Serializers
Anytime you use a type within a communication Fish-Networking automatically recognizes you wish to send the type over the network, and will create a serializer for it. You do not need to perform any extra steps for this process, but if you would like to exclude fields from being serialized use [System.NonSerialized] above the field.

For example, Name and Level will be sent over the network but not Victories.

```csharp
public class PlayerStat
{
public string Name;
public int Level;
[System.NonSerialized]
public int Victories;
}

[ServerRpc]
public void RpcPlayerStats(PlayerStat stats){}
```
Fish-Networking is also capable of serializing inherited values. In the type MonsterStat below, Health, Name, and Level will automatically serialize.

```csharp
public class Stat
{
public float Health;
}
public class MonsterStat : Stat
{
public string Name;
public int Level;
}
```
In very rare cases a data type cannot be automatically serialized; a Sprite is a good example of this. It would be very difficult and expensive to serialize the actual image data and send that over the network. Instead, you could store your sprites in a collection and send the collection index, or perhaps you could create a custom serializer.

# Custom Serializers
Custom serializers are useful where an automatic serializer may not be possible, or where you want data to be serialized in a specific manner.

When creating a custom serializer there are a few important things to remember. When you follow the proper steps your custom serializer will be found and used by Fish-Networking. Your custom serializers can also override automatic serializers, but not included ones.

Your method must be static, and within a static class.

Writing method names must begin with Write.

Reading method names must begin with Read.

The first parameter must be this Writer for writers, and this Reader for readers.

Data must be read in the same order it is written.

Although Vector2 is already supported, the example below uses a Vector2 for simplicity sake.

```csharp
//Write each axis of a Vector2.
public static void WriteVector2(this Writer writer, Vector2 value)
{
writer.WriteSingle(value.x);
writer.WriteSingle(value.y);
}

//Read and return a Vector2.
public static Vector2 ReadVector2(this Reader reader)
{
return new Vector2()
{
x = reader.ReadSingle(),
y = reader.ReadSingle()
};
}
```
Custom serializers are more commonly used for conditional situations where what you write may change depending on the data values. Here is a more complex example where certain data is only written when it's needed.

```csharp
/* This is the type we are going to write.
* We will save data and populate default values
* by not writing energy/energy regeneration if
* the enemy does not have energy. */
  public struct Enemy
  {
  public bool HasEnergy;
  public float Health;
  public float Energy;
  public float EnergyRegeneration;
  }

public static void WriteEnemy(this Writer writer, Enemy value)
{
writer.WriteBoolean(value.HasEnergy);
writer.WriteSingle(value.Health);

    //Only need to write energy and energy regeneration if HasEnergy is true.
    if (value.HasEnergy)
    {
        writer.WriteSingle(value.Energy);
        writer.WriteSingle(value.EnergyRenegeration);
    }
}

public static Enemy ReadEnemy(this Reader reader)
{
Enemy e = new Enemy();
e.HasEnergy = reader.ReadBoolean();
e.Health = reader.ReadSingle();

    //If there is energy also read energy values.
    if (e.HasEnergy)
    {
        e.Energy = reader.ReadSingle();
        e.EnergyRenegeration = reader.ReadSingle();
    }

    return e;
}
```
Often when creating a custom serializer you want to use it across your entire project, and all assemblies. Without taking any action further your custom serializer would only be used on the assembly it is written. Presumably, that's probably not what you want.

But making a custom serializer work across all assemblies is very simple. Simply add the [UseGlobalCustomSerializer] attribute of the type your custom serializer is for, and done!

Example:

```csharp
[UseGlobalCustomSerializer]
public struct Enemy
{
public bool HasEnergy;
public float Health;
public float Energy;
public float EnergyRegeneration;
}
```

# Interface Serializers
## General
Interfaces are very commonly used in most Unity Projects. Since Interfaces are not classes, even if the interface only uses serializable fields, a custom serializer is still needed in order for SyncTypes, and RPCs to serialize them properly over the network.

## Serializing the Interfaces Entire Class
In most cases you will want to interrogate an Interface as what its class type is, and serialize the entire types class over the network. This allows you to interrogate the interface later on the receiving client/server and have the data match what the sender has at the time it was sent as well.

If the Interface is a NetworkBehaviour you might as well send it as one because Serializing them over the network is only sending an ID for the receiving client to look up. Very Little network traffic, and you still get all of the data!

## Creating The Writer
Since Interfaces are not classes you must design the writer to be able to interrogate the class the Interface is, and serialize the class over the network. If an interface can be many types of classes, you will need to account for each class the Interface can be.

```csharp
public static void WriteISomething(this Writer writer, ISomething som)
{
if(som is ClassA c1)
{
// 1 will be the identifer for the reader that this is a ClassA.
writer.WriteByte(1);
writer.Write(c1);
}
else if(som is ClassB c2)
{
//2 will be the identifier for the reader that this is a ClassB.
writer.WriteByte(2)
writer.Write(c2);
}
}
```
## Creating The Reader
When reading the interface, we have to read the byte that identifies what class the interface actually is first. Then use the reader to read that classes data. Finally casting it as the interface we need.

```csharp
public static ISomething ReadISomething(this Reader reader)
{   
//Gets the byte of what class type we should be reading the next bit of data as.
byte clsType = reader.ReadByte();

    //Remember we assigned 1 to be ClassA.
    if(clsType == 1)
        return reader.Read<ClassA>();
    //And 2 for ClassB.
    else if(clsType == 2)
        return reader.Read<ClassB>();

    //Fall through, unhandled. This would be bad.
    return default;
}
```
## Serializing Only The Interfaces Properties
Sometimes you may only want to serialize just the Interface properties over the network, just keep in mind that if you cast it as the Type it actually is on the receiving client, the values of fields not apart of the interface will be their default values!

## Creating The Writer
You still will have to use an Identifier to send what class the Interface is, but we will not be sending the entire class over the network. Just the Interface Properties.

```csharp
public interface ISomething
{
string Name;
int Health;
ushort Level;
}
```
```csharp
public static void WriteISomething(this Writer writer, ISomething som)
{
//Defining a blank Class Type Indentifier
byte clsType = 0; //Default

    if(som is CustomClass1 cc1)
        writer.WriteByte(1);    
    else if(som is CustomClass2 cc2)
        writer.WriteByte(2);
    //Fall through, indicating unknown type.
    else
        writer.WriteByte(0);
    
    //Remember the order the data is written, is the order it must be read.
    writer.WriteString(som.Name);
    writer.WriteInt32(som.Health);
    writer.WriteUInt16(som.Level);
}
```
## Creating The Reader
When reading, we will get the class type from the identifier, create a new class, cast the class as the interface, and then assign the custom serialized values to the interface!

```csharp
public static ISomething ReadISomething(this Reader reader)
{
/* Getting the Class Type Indentifier.
* Read all values first. to clear out the
* reader. */
byte clsType = reader.ReadByte();
string name = reader.ReadString();
int health = reader.ReadInt32();
ushort level = reader.ReadUInt16();

    ISomething som = default;
    //Check to see what class the interface is
    if(clsType == 1)
        som = new CustomClass1();
    else if(clasType == 2)
        som = new CustomClass2();
    
    //Value was not set, so we cannot populate it.
    if(som == default(ISomething))
        return null;
    
    som.Name = name;
    som.Health = health;
    som.Level = level;
    
    return som;
}
```

# Inheritance Serializers
Another frequently asked question is how to handle serialization for classes which are inherited by multiple other classes. These are often used to allow RPCs to use the base class as a parameter, while permitting other inheriting types to be used as arguments. This approach is similar to Interface serializers.

## Class Example
Here is an example of a class you want to serialize, and two other types which inherit it.

```csharp
public class Item : ItemBase
{
public string ItemName;
}

public class Weapon : Item
{
public int Damage;
}

public class Currency : Item
{
public byte StackSize;
}

//This is a wrapper to prevent endless loops in
//your serializer. Why this is used is explained
//further down.
public abstract class ItemBase {}

```
Using an RPC which can take all of the types above might look something like this.

```csharp
public void DoThing()
{
Weapon wp = new Weapon()
{
Itemname = "Dagger",
Damage = 50,
};
ObsSendItem(wp);
}

[ObserversRpc]
private void ObsSendItem(ItemBase ib)
{
//You could check for other types or just convert it without checks
//if you know it will be Weapon.
//EG: Weapon wp = (Weapon)ib;
if (ib is Weapon wp)
Debug.Log($"Recv: Item name {wp.ItemName}, damage value {wp.Damage}.");
}
````
## Creating The Writer
Since you are accepting ItemBase through your RPC you must handle the different possibilities of what is being sent. Below is a serializer which does just that.

When using this approach it is very important that you check for the child-most types first.

For example: Weapon is before Item, and so is Currency, so those two are checked first. Just as if you had Melee : Weapon, then Melee would be before Weapon, and so on.

```csharp
public static void WriteItembase(this Writer writer, ItemBase ib)
{
if (ib is Weapon wp)
{
// 1 will be the identifer for the reader that this is Weapon.
writer.WriteByte(1);
writer.Write(wp);
}
else if (ib is Currency cc)
{
writer.WriteByte(2)
writer.Write(cc);
}
else if (ib is Item it)
{
writer.WriteByte(3)
writer.Write(it);
}
}

public static ItemBase ReadItembase(this Reader reader)
{
byte clsType = reader.ReadByte();
//These are still in order like the write method, for
//readability, but since we are using a clsType indicator
//the type is known so we can just compare against the clsType.
if (clsType == 1)
return reader.Read<Weapon>();
else if (clsType == 2)
return reader.Read<Currency>();
else if (clsType == 1)
return reader.Read<Item>();
//Unhandled, this would probably result in read errors.
else
return null;
}

```
You can still create custom serializers for individual classes in addition to encapsulating ones as shown! If for example you had a custom serializer for Currency then using the code above would use your serializer for Currency rather than the one Fish-Networking generates.

Finally, disclosing why we made the ItemBase class. The sole purpose of ItemBase is to prevent an endless loop in the reader. Imagine if we were able to return only Item, and we were also using that as our base. Your reader might look like this...

```csharp
public static Item ReadItem(this Reader reader)
{
byte clsType = reader.ReadByte();
//These are still in order like the write method, for
//readability, but since we are using a clsType indicator
//the type is known so we can just compare against the clsType.
if (clsType == 1)
return reader.Read<Weapon>();
else if (clsType == 2)
return reader.Read<Currency>();
else if (clsType == 1)
return reader.Read<Item>();
//Unhandled, this would probably result in read errors.
else
return null;
}
```
The line return reader.Read<Item>(); is the problem. By calling read on the same type as the serializer you would in result call the ReadItem method again, and then the line return reader.Read<Item>(); and then ReadItem again, and then, well you get the idea.

Having a base class, in our case ItemBase, which cannot be returned ensures no endless loop.

# Addressables
Both addressable scenes and prefabs work over the network.

## Scene Addressables
Scene addressables utilize Fish-Networking's Custom Scene Processors. With a few overrides you can implement addressable scenes using this guide.

## Prefab Addressables
To work reliably each addressables package must have a unique ushort Id, between 1 and 65535. Never use 0 as the Id, as the preconfigured SpawnablePrefabs use this Id. You may assign your addressable Ids however you like, for instance using a dictionary that tracks your addressable names with Ids.

Registering addressable prefabs with Fish-Networking is easy once each package has been given an Id.

The code below shows one way of loading and unloading addressable prefabs for the network.

```csharp
/// <summary>
/// Reference to your NetworkManager.
/// </summary>
private NetworkManager _networkManager => InstanceFinder.NetworkManager;
/// <summary>
/// Used to load and unload addressables in async.
/// </summary>
private AsyncOperationHandle<IList<GameObject>> _asyncHandle;

/// <summary>
/// Loads an addressables package by string.
/// You can load whichever way you prefer, this is merely an example.
/// </summary>
public IEnumerator LoadAddressables(string addressablesPackage)
{
/* FishNet uses an Id to identify addressable packages
* over the network. You can set the Id to whatever, however
* you like. A very simple way however is to use our GetStableHash
* helper methods to return a unique key for the package name.
* This does require the package names to be unique. */
ushort id = addressablesPackage.GetStableHash16();

    /* GetPrefabObjects will return the prefab
     * collection to use for Id. Passing in true
     * will create the collection if needed. */
    SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects)_networkManager.GetPrefabObjects<SinglePrefabObjects>(id, true);

    /* Get a cache to store networkObject references in from our helper object pool.
     * This is not required, you can make a new list if you like. But if you
     * prefer to prevent allocations FishNet has the really helpful CollectionCaches
     * and ObjectCaches, as well Resettable versions of each. */
    List<NetworkObject> cache = CollectionCaches<NetworkObject>.RetrieveList();

    /* Load addressables normally. If the object is a NetworkObject prefab
     * then add it to our cache! */
    _asyncHandle = Addressables.LoadAssetsAsync<GameObject>(addressablesPackage, addressable =>
    {
        NetworkObject nob = addressable.GetComponent<NetworkObject>();
        if (nob != null)
            cache.Add(nob);
    });
    yield return _asyncHandle;
    
    /* Add the cached references to spawnablePrefabs. You could skip
     * caching entirely and just add them as they are read in our LoadAssetsAsync loop
     * but this saves more performance by adding them all at once. */
    spawnablePrefabs.AddObjects(cache);

    //Optionally(obviously, do it) store the collection cache for use later. We really don't like garbage!
    CollectionCaches<NetworkObject>.Store(cache);
}

/// <summary>
/// Loads an addressables package by string.
/// </summary>
public void UnoadAddressables(string addressablesPackage)
{
//Get the Id the same was as we did for loading.
ushort id = addressablesPackage.GetStableHash16();

    /* Once again get the prefab collection for the Id and
     * clear it so that there are no references of the objects
     * in memory. */
    SinglePrefabObjects spawnablePrefabs = (SinglePrefabObjects)_networkManager.GetPrefabObjects<SinglePrefabObjects>(id, true);
    spawnablePrefabs.Clear();
    //You may now release your addressables!
    Addressables.Release(_asyncHandle);
}
```

# Prediction
Prediction is the act of server-authoritative actions while allowing clients to move in real-time without delay.

## What Is Client-Side Prediction
Client-Side Prediction allows clients to perform actions in real-time while maintaining server authority.

Client-side prediction is a technique used to move in real-time on clients, providing responsiveness actions, while also ensuring such actions cannot be cheated. From here out, we will refer to client-side prediction as CSP.

During your development you may also hear the term 'server authoritative movement'. CSP is a form of server authoritative movement, but they are not the same.

As mentioned CSP allows the client to move in real-time while also ensuring they cannot cheat. Some server authoritative movements will ensure the client cannot cheat, but does so by moving on the server only and relaying the results to clients. While both work, the latter of moving on the server and then relaying will result in the client to have a delay based on their latency.

Having such a delay would be unfair to those with higher latency, and could ruin the experience for players if your game is expected to have responsive movement. This is why having CSP built into Fish-Networking is so great!

## Controlling An Object
Learn how to create a predicted object that the owner or server can control.

### Data Structures
Implementing prediction is done by creating a replicate and reconcile method, and making calls to the methods accordingly.

Your replicate method will take inputs you wish to run on the owner, server, and other clients if using state forwarding. This would be any input needed for your controller such as jumping, sprinting, movement direction, and could even include other mechanics such as holding a fire button.

The reconcile method takes a state of the object after a replicate is performed. This state is used to make corrections in the chance of de-synchronizations. For example, you may send back health, velocity, transform position and rotation, so on.

It's also worth mentioning if you are going to allocate in your structures it could be beneficial to utilize the Dispose callback, which will run as the data is being discarded.

Here are the two structures containing basic mechanics for a rigidbody.

```csharp
public struct ReplicateData : IReplicateData
{
public bool Jump;
public float Horizontal;
public float Vertical;
public ReplicateData(bool jump, float horizontal, float vertical) : this()
{
Jump = jump;
Horizontal = horizontal;
Vertical = vertical;
}

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

public struct ReconcileData : IReconcileData
{
//PredictionRigidbody is used to synchronize rigidbody states
//and forces. This could be done manually but the PredictionRigidbody
//type makes this process considerably easier. Velocities, kinematic state,
//transform properties, pending velocities and more are automatically
//handled with PredictionRigidbody.
public PredictionRigidbody PredictionRigidbody;

    public ReconcileData(PredictionRigidbody pr) : this()
    {
        PredictionRigidbody = pr;
    }

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}
```

### Preparing To Call Prediction Methods
Typically speaking you would want to run your replicate(or inputs) during OnTick. When you send the reconcile depends on if you are using physics bodies or not.

When using physics bodies, such as a rigidbody, you would send the reconcile during OnPostTick because you want to send the state after the physics have simulated your replicate inputs. See the TimeManager API for more details on tick and physics event callbacks.

Non-physics controllers can also send in OnTick, since they do not need to wait for a physics simulation to have the correct outcome after running inputs.

The code below shows which callbacks and API to use for a rigidbody setup.

You may need to modify move and jump forces depending on the shape, drag, and mass of your rigidbody.

```csharp
//How much force to add to the rigidbody for jumps.
[SerializeField]
private float _jumpForce = 8f;
//How much force to add to the rigidbody for normal movements.
[SerializeField]
private float _moveForce = 15f;
//PredictionRigidbody is set within OnStart/StopNetwork to use our
//caching system. You could simply initialize a new instance in the field
//but for increased performance using the cache is demonstrated.
public PredictionRigidbody PredictionRigidbody;
//True if to jump next replicate.
private bool _jump;

private void Awake()
{
PredictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
}
private void OnDestroy()
{
ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref PredictionRigidbody);
}
public override void OnStartNetwork()
{
base.TimeManager.OnTick += TimeManager_OnTick;
base.TimeManager.OnPostTick += TimeManager_OnPostTick;
}

public override void OnStopNetwork()
{
base.TimeManager.OnTick -= TimeManager_OnTick;
base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
}
```
### Calling Prediction Methods
For our described demo, below is how you would gather input for your replicate and reconcile methods.

Update is used to gather inputs which are only fired for a single frame. Ticks do not occur every frame, but rather at the interval of your TickDelta, much like FixedUpdate works. While the code below only uses Update for single frame inputs there is nothing stopping you from using it for held inputs as well.

```csharp
private void Update()
{
    if (base.IsOwner && Input.GetKeyDown(KeyCode.Space)) _jump = true;
}
```
OnTick will now be used to build our replicate data. A separate method of 'CreateReplicateData' is not needed to create the data but is done to organize our code better.

When attempting to create the replicate data we return with default if not the owner of the object. Server receives and runs inputs from the owner so it does not need to create datas, and when clients do not own an object they will get the input for it from the server, as forwarded by other clients if using state forwarding. When not using state forwarding default should still be used in this scenario, but clients will not run replicates on non-owned objects. You can also run inputs on the server if there is no owner; using base.HasAuthority would probably be best for this. See Checking Ownership for more information.

```csharp
private void TimeManager_OnTick()
{
    RunInputs(CreateReplicateData());
}

private ReplicateData CreateReplicateData()
{
    if (!base.IsOwner) return default;

    //Build the replicate data with all inputs which affect the prediction.
    float horizontal = Input.GetAxisRaw("Horizontal");
    float vertical = Input.GetAxisRaw("Vertical");
    ReplicateData md = new ReplicateData(_jump, horizontal, vertical);
    _jump = false;

    return md;
}
```
Now implement your replicate method. The name may be anything but the parameters shown are required. The first is what we pass in, the remainder are set at runtime. Although, you absolutely may change the default channel used in the parameter or even at runtime.

For example, it could be beneficial to send an input as reliable if you absolutely want to ensure it's not dropped due to network issues.

```csharp
[Replicate]
private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
/* ReplicateState is set based on if the data is new, being replayed, ect.
* Visit the ReplicationState enum for more information on what each value
* indicates. At the end of this guide a more advanced use of state will
* be demonstrated. */

    //Be sure to always apply and set velocties using PredictionRigidbody
    //and never on the rigidbody itself; this includes if also accessing from
    //another script.
    Vector3 forces = new Vector3(data.Horizontal, 0f, data.Vertical) * _moveRate;
    PredictionRigidbody.AddForce(forces);

    if (data.Jump)
    {
        Vector3 jmpFrc = new Vector3(0f, _jumpForce, 0f);
        PredictionRigidbody.AddForce(jmpFrc, ForceMode.Impulse);
    }
    //Add gravity to make the object fall faster. This is of course
    //entirely optional.
    PredictionRigidbody.AddForce(Physics.gravity * 3f);
    //Simulate the added forces.
    //Typically you call this at the end of your replicate. Calling
    //Simulate is ultimately telling the PredictionRigidbody to iterate
    //the forces we added above.
    PredictionRigidbody.Simulate();
}
```
On non-owned objects a number of replicates will arrive as ReplicateState Created, but will contain default values. This is our PredictionManager.RedundancyCount feature working.

This is normal and indicates that the client or server had gracefully stopped sending states as there is no new data to send. This can be useful if you are Predicting States.

Now the reconcile must be sent to clients to perform corrections. Only the server will actually send the reconcile but be sure to call CreateReconcile no matter if client, server, owner or not; this is to future proof an upcoming feature. Unlike our CreateReplicateData method, using CreateReconcile is not optional.

```csharp
private void TimeManager_OnPostTick()
{
CreateReconcile();
}

//Create the reconcile data here and call your reconcile method.
public override void CreateReconcile()
{
//We must send back the state of the rigidbody. Using your
//PredictionRigidbody field in the reconcile data is an easy
//way to accomplish this. More advanced states may require other
//values to be sent; this will be covered later on.
ReconcileData rd = new ReconcileData(PredictionRigidbody);
//Like with the replicate you could specify a channel here, though
//it's unlikely you ever would with a reconcile.
ReconcileState(rd);
}
```
Reconciling only a rigidbody state is very simple.

```csharp
[Reconcile]
private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
{
//Call reconcile on your PredictionRigidbody field passing in
//values from data.
PredictionRigidbody.Reconcile(data.PredictionRigidbody);
}
```

## Non-Controlled Object
A very simple script for keeping non-controlled objects in synchronization with the prediction system.

Many games will require physics bodies to be networked, even if not controlled by players or the server. These objects can also work along-side the new state system by adding a basic prediction script on them.

It's worth noting that you can also 'control' non-owned objects on the server by using base.HasAuthority. This was discussed previously here.

Sample Script
Below is a full example script to synchronize a non-controlled rigidbody. Since the rigidbody is only reactive, input polling is not needed. Otherwise you'll find the data structures are near identical to the ones where we took input.

It is strongly recommended to review the controlling objects guide for additional notes in understanding the code below.

```csharp
public class RigidbodySync : NetworkBehaviour
{
//Replicate structure.
public struct ReplicateData : IReplicateData
{
//The uint isn't used but Unity C# version does not
//allow parameter-less constructors we something
//must be set as a parameter.
public ReplicateData(uint unused) : this() {}
private uint _tick;
public void Dispose() { }
public uint GetTick() => _tick;
public void SetTick(uint value) => _tick = value;
}
//Reconcile structure.
public struct ReconcileData : IReconcileData
{
public PredictionRigidbody PredictionRigidbody;

        public ReconcileData(PredictionRigidbody pr) : this()
        {
            PredictionRigidbody = pr;
        }
    
        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    //Forces are not applied in this example but you
    //could definitely still apply forces to the PredictionRigidbody
    //even with no controller, such as if you wanted to bump it
    //with a player.
    private PredictionRigidbody PredictionRigidbody;
    
    private void Awake()
    {
        PredictionRigidbody = ObjectCaches<PredictionRigidbody>.Retrieve();
        PredictionRigidbody.Initialize(GetComponent<Rigidbody>());
    }
    private void OnDestroy()
    {
        ObjectCaches<PredictionRigidbody>.StoreAndDefault(ref PredictionRigidbody);
    }

    //In this example we do not need to use OnTick, only OnPostTick.
    //Because input is not processed on this object you only
    //need to pass in default for RunInputs, which can safely
    //be done in OnPostTick.
    public override void OnStartNetwork()
    {
        base.TimeManager.OnPostTick += TimeManager_OnPostTick;
    }

    public override void OnStopNetwork()
    {
        base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
    }

    private void TimeManager_OnPostTick()
    {
        RunInputs(default);
        CreateReconcile();
    }

    [Replicate]
    private void RunInputs(ReplicateData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        //If this object is free-moving and uncontrolled then there is no logic.
        //Just let physics do it's thing.	
    }

    //Create the reconcile data here and call your reconcile method.
    public override void CreateReconcile()
    {
        ReconcileData rd = new ReconcileData(PredictionRigidbody);
        ReconcileState(rd);
    }

    [Reconcile]
    private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
    {
        //Call reconcile on your PredictionRigidbody field passing in
        //values from data.
        PredictionRigidbody.Reconcile(data.PredictionRigidbody);
    }
}
```

## Understanding ReplicateState
Being familiar with what each state means will help you fine-tune your gameplay on spectated objects.

If you need a refresher on what each state means see our API or simply navigate to the ReplicateState enum in your code editor.

Each state is well commented in the source code as well the API, so rather than cover in detail what each state means, examples of how you might apply certain states will be shown.

There are several extensions to check states. For example, state.IsFuture() will return true if the state is ReplayedFuture or CurrentFuture. See ReplicateStateExtensions for all built-in extensions.

* Invalid. An invalid ReplicateState should never occur. This would imply internally Fish-Networking failed to properly set the state.
* Created. When a state is created it is known to be true. This means a created state has absolutely no chance to contain incorrect information as it's what was received from the client, or run locally for the owner.
* Predicted. These states are marked obsolete because they are currently not used, though we may implement them in the future. The documentation elaborates on what these might indicate when they are implemented.
* Current. A state leading with 'Current' simply indicates the input is being run outside of a reconcile. For example, calling your replicate from OnTick would hold the state as Current.
* Future. Future states are when the client has not received inputs for those ticks from the server yet. Remember that part of client-side prediction is the client always moving in real-time locally, but like any networking library, they still must wait for data to arrive from the server before it can be known(Created). Given the client is moving in real-time, they are always ahead a number of ticks more than the last data they received from the server. Latency will determine how much further the client is ahead, as more latency means the client will receive known states later. This is where you may predict server states locally; our  Predicting States covers this more thoroughly.
* Replayed. States leading with the 'Replayed' prefix indicate that the data is being performed within a reconcile. The server never replays states as it has no need to reconcile, but for clients states will be replayed on owned and spectated objects.

A replayed states does not mean that data is known(Created), it simply indicates the states are run during a reconcile.

Replayed states will always be ReplayedCreated on objects the client owns. This is because the owning client has real-time information on the states, since they are the ones creating them. On non-owned objects clients will see a number of states as ReplayedFuture; this was elaborated on as why just above.

An common problem developers run into is where data in their replicate methods flip when the state is Created vs Future, and the developers logic does not accommodate the differences. Here is example of a spectated object might have 'Sprint' held in their ReplayedCreated state, but show as not held in a ReplayedFuture state. Since future states indicate data not yet known, said data will be default. When the client runs logic on default data unexpected behavior may happen.

Below is an example of code that might cause a problem.

```csharp
[Replicate]
private void MovePlayer(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
//Move twice as fast if sprinting.
float moveMultiplier = (data.Sprint) ? 2f : 1f;
//This value is irrelevant other than showing we are moving in a direction.
Vector3 moveDirection = Vector3.right;

    transform.position += moveDirection * moveMultiplier;
}
```
We know that a future state will have default data, in result Sprint will be false for future states. If Sprint were true previously in a Created state then the player would move faster during the created state, then slower during the future state, even though the player could very well could still be sprinting. At the very least this would cause a positional de-synchronization, which might be covered up by the smoothing. But what if the code was slightly more complex like this ...

```csharp
[Replicate]
private void MovePlayer(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
//Move twice as fast if sprinting.
float moveMultiplier = (data.Sprint) ? 2f : 1f;
//This value is irrelevant other than showing we are moving in a direction.
Vector3 moveDirection = Vector3.right;

    transform.position += moveDirection * moveMultiplier;
    ShowVFX(data.Sprint);
}
```
Now not only the position is being updated with false for Sprint, but the VFX are. Our built-in graphical smoothing only handles interpolating the graphical object, so your visual effects would regularly flicker between sprinting and not.

The resolution for such a case is straight forward enough. You can either predict the state, a reminder that this is discussed in the next guide, or simply do nothing if the state is future. Which you choose is entirely relevant to your needs.

Let's see what it might look like if you do not want to predict states.

```csharp
[Replicate]
private void MovePlayer(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
if (state.IsFuture())
return;
//Move twice as fast if sprinting.
float moveMultiplier = (data.Sprint) ? 2f : 1f;
//This value is irrelevant other than showing we are moving in a direction.
Vector3 moveDirection = Vector3.right;

    transform.position += moveDirection * moveMultiplier;
    ShowVFX(data.Sprint);
}
```
Rather than perform logic at all, we simply exit the method if the state is in the future. This will eliminate the chance of running logic based on potentially incorrect inputs.

Doing something along these lines does have disadvantages. When you are not predicting into the future the object will remain behind based on your clients latency. This would potentially in result cause the client to react differently depending on what they see. To re-iterate, what you may want to do is entirely relevant to your needs.

Below is are examples of how you might want to keep a rigidbody in the past, either by canceling it's forces or not applying additional forces.

'In the past' is a simple way of us saying, not current with the server and/or not predicted.

This example shows simply exiting the method if in the future. While doing so the rigidbody will continue to move using it's current velocities as well be impacted by anything which might affect it. In a way, this is a simple approach to offering some basic future prediction on a rigidbody.

```csharp
if (state.IsFuture()) return;
```
The next example is canceling forces entirely on the rigidbody if the state is in the future. This keeps the rigidbody in the past for the client by not allowing it to move beyond what we know to be true.

```csharp
if (state.IsFuture())
{
//This assumes you are using PredictionRigidbody, which you should be.
_myPredictionRigidbody.Velocity(Vector3.Zero);
_myPredictionRigidbody.AngularVelocity(Vector3.Zero);
return;
]
```
Understandably how to handle future states might be confusing at first. As you develop for your game you'll have a better idea of what feels best for the player, determining how to implement future states.

## Predicting States
Due to the unpredictability of the Internet inputs may drop or arrive late. Predicting states is a simple way to compensate for these events.

If your game is fast-paced and reaction based, even if not using physics, predicting states can be useful to predict the next inputs you'll get from the server before you actually get them.

Both the server and client can predict inputs.

The server may predict inputs to accommodate for clients with unreliable connections.

Clients would predict inputs on objects they do not own, or as we often call them "spectated objects".

By predicting inputs you can place objects in the future of where you know them to be, and even make them entirely real-time with the client. Keep in mind however, when predicting the future, if you guess wrong there will be a de-synchronization which may be seen as jitter when it's corrected in a reconcile.

Below is an example of a simple replicate method.

```csharp
//What data does is irrelevant in this example.
//We're only interested in how to predict a future state.
[Replicate]
private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
float delta = (float)base.TimeManager.TickDelta;
transform.position += new Vector3(data.Horizontal, 0f, data.Vertical) * _moveRate * delta;
}

```
Before we go any further you must first understand what each ReplicateState is. These change based on if input is known, replaying inputs or not, and more. You can check out the ReplicateState API which will explain thoroughly. You can also find this information right in the source of FishNet.

If you've read through ReplicateState and do not fully understand them please continue reading as they become more clear as this guide progresses. You can also visit us on Discord for questions!

Covered in the ReplicateStates API: CurrentCreated will only be seen on clients if they own the object. When inputs are received on spectated objects clients run them in the reconcile, which will have the state ReplayedCreated. Clients will also see ReplayedFuture and CurrentFuture on spectated objects.

A state ending in 'Future' essentially means input has not been received yet, and these are the states you could predict.

Let's assume your game has a likeliness that players will move in the same direction regularly enough. If a player was holding forward for three ticks the input would look like this...

```csharp
(data.Vertical == 1)
(data.Vertical == 1)
(data.Vertical == 1)

```
But what if one of the inputs didn't arrive, or arrived late? The chances of inputs not arriving at all are pretty slim, but arriving late due to network variance is extremely common. If perhaps an input did arrive late the values may appear as something of this sort...

```csharp
(data.Vertical == 1)
(data.Vertical == 1)
(data.Vertical == 0) //Didn't arrive here, but will arrive late next tick.
(data.Vertical == 1) //This was meant to arrive the tick before, but arrived late.

```
Because of this interruption the player may seem to move forward twice, pause, then forward again. Realistically to help cover this up you will have interpolation on your graphicalObject as shown under the prediction settings for NetworkObject. The PredictionManager also offers QueuedInputs which can give you even more of a buffer. For the sake of this guide though we're going to pretend both of those didn't get the job done, and you need to account for the late input.

Below is a simple way to track and use known inputs to create predicted ones.

```csharp
private ReplicateData _lastCreatedInput = default;

[Replicate]
private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
//If inputs are not known. You could predict
//all the way into CurrentFuture, which would be
//real-time with the client. Though the more you predict
//in the future the more you are likely to mispredict.
if (state.IsFuture())
{
uint lastCreatedTick = _lastCreatedInput.GetTick();
//If it's only been 2 ticks since the last created
//input then run the logic below.
//This essentially means if the last created tick
//was 100, this logic would run if the future tick was 102
//or less. This is an example of a basic approach to only
//predict a certain number of inputs.
uint thisTick = data.GetTick();
if ((data.GetTick() - lastCreatedTick) <= 2)
{
//We do not necessarily want to predict all states.
//For example, it probably wouldn't make sense to predict
//multiple jumps in a row. In this example only the movement
//inputs are predicted.
data.Vertical = _lastCreatedInput.Vertical;
}
}
//If created data then set as lastCreatedInput.
else if (state == ReplicateState.ReplayedCreated)
{
//If ReplicateData contains fields which could generate garbage you
//probably want to dispose of the lastCreatedInput
//before replacing it. This step is optional.
_lastCreatedInput.Dispose();
//Assign newest value as last.
_lastCreatedInput = data;
}

    float delta = (float)base.TimeManager.TickDelta;
    transform.position += new Vector3(data.Horizontal, 0f, data.Vertical) * _moveRate * delta;
}

```
If your ReplicateData allocates do not forget to dispose of the lastCreatedInput when the network is stopped.

## Advanced Controls
This guide supplements the basic prediction guide by showing how to introduce more complexities to your controls.

Be sure to review the previous guides in this section before reviewing this page.

### Guide Goal
Implementing additional features into your prediction is much like you would code in a single player game, only remembering to reconcile anything that could de-synchronize the prediction.

In this guide a psuedo ground check before a jump will be added, as well a sprint function.

Sprinting and Ground Checks
First the ReplicateData needs to be updated to contain our sprint action, which will rely on a stamina mechanic. Not much changed other than we added the Sprint boolean and set it using the constructor.

```csharp
public struct ReplicateData : IReplicateData
{
public bool Jump;
public bool Sprint;
public float Horizontal;
public float Vertical;
public ReplicateData(bool jump, bool sprint, float horizontal, float vertical) : this()
{
Jump = jump;
Sprint = sprint;
Horizontal = horizontal;
Vertical = vertical;
}

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

```
After updating the ReplicateData we need to poll for the sprint key when creating the data, like you would in most games.

We are re-using methods from our previous guides so much of this should be familiar.

```csharp
private ReplicateData CreateReplicateData()
{
if (!base.IsOwner)
return default;

    //Build the replicate data with all inputs which affect the prediction.
    float horizontal = Input.GetAxisRaw("Horizontal");
    float vertical = Input.GetAxisRaw("Vertical");

    /* Sprint if left shift is held.
    * You do not necessarily have to perform security checks here.
    * For example, it was mentioned sprint will rely on stamina, we
    * are not checking the stamina requirement here. You certainly could
    * as a precaution but this is only building the replicate data, not where
    * the data is actually executed, which is where we want
    * the check. */
    bool sprint = Input.GetKeyDown(KeyCode.LeftShift);
    
    ReplicateData md = new ReplicateData(_jump, sprint, horizontal, vertical);
    _jump = false;

    return md;
}

```
Declare a stamina float in your class.

```csharp
//Current stamina for the player.
private float _stamina;

```
Now use our new Sprint bool and stamina field to apply sprinting within the replicate method.

```csharp
[Replicate]
private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
{
float delta = (float)base.TimeManager.TickDelta;
//Regenerate stamina at 3f per second.
_stamina += (3f * delta);
//How much it cost to use sprint per delta.
//This causes sprint to use stamina twice as fast
//as the stamina recharges.
float sprintCost = (6f * delta);
Vector3 forces = new Vector3(data.Horizontal, 0f, data.Vertical) * _moveRate;
//If sprint is held and enough stamina exist then multiple forces.
if (data.Sprint && _stamina >= sprintCost)
{    
//Reduce stamina by cost.
_stamina -= sprintCost;
//Increase forces by 30%.
forces *= 1.3f;
}


    /* You should check for any changes in replicate like we do
    * with stamina. Recall how it was said checking stamina when
    * gathering the inputs is not so important, but doign so in the replicate
    * is what grants server authority, as well makes prediction function
    * properly with corrections and rollbacks. */
    
    /* Now check if to jump. IsGrounded() does not exist, we're going to
    * pretend it uses a raycast or overlap to check. */
    if (data.Jump && IsGrounded())
    {
        Vector3 jmpFrc = (Vector3.up * _jumpForce);
       PredictionRigidbody.AddForce(jmpFrc, ForceMode.Impulse);
    }
    
    //Rest of the code remains the same.
}

```
If a value can affect your prediction do not store it outside the replicate method, unless you are also reconciling the value. An exception applies if you are setting the value inside your replicate method.

This is a very important detail to remember, and is discussed further below.

Reconciling only a rigidbody state is very simple.

```csharp
[Reconcile]
private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
{
//Call reconcile on your PredictionRigidbody field passing in
//values from data.
PredictionRigidbody.Reconcile(data.PredictionRigidbody);
}

```
If you are using multiple rigidbodies you at the very least need to reconcile their states as well. You can do so quickly by adding a RigidbodyState for each rigidbody to your reconcile.

If you are also applying forces to these rigidbodies be sure to use PredictionRigidbody with them, and reconcile the PredictionRigidbody instead of RigidbodyState.

Changes To Reconcile
Because objects can reconcile to previous states it's fundamental to also reconcile any values stored outside the replicate method. Imagine if you had 10f stamina, enough to sprint, and did so successfully on the server and owner. After your sprint you only had 1f stamina, not enough to sprint further.

If you were to reconcile without resetting stamina to it's previous values then you would still be at 1f stamina after reconciling. Your replayed inputs, which previously allowed the sprint, would not sprint because you now lacked the needed stamina. In result of this, you would have a de-synchronization which would most likely be seen as jitter.

Including more variables in your prediction is fortunately easy enough. All you have to do is update your reconcile to include states or your new values or variables.

Added to our ReconcileData structure is a Stamina float.

```csharp
public struct ReconcileData : IReconcileData
{
public PredictionRigidbody PredictionRigidbody;
public float Stamina;

    public ReconcileData(PredictionRigidbody pr, float stamina) : this()
    {
        PredictionRigidbody = pr;
        Stamina = stamina;
    }

    private uint _tick;
    public void Dispose() { }
    public uint GetTick() => _tick;
    public void SetTick(uint value) => _tick = value;
}

```
Then of course we must include the current value of stamina within our created reconcile data.

```csharp
public override void CreateReconcile()
{
ReconcileData rd = new ReconcileData(PredictionRigidbody, _stamina);
ReconcileState(rd);
}

```
Very last you must utilize the new reconcile data to reset the stamina state.

```csharp
[Reconcile]
private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
{
PredictionRigidbody.Reconcile(data.PredictionRigidbody);
_stamina = data.Stamina;
}

```
With minor additions to the code you now have an authoritative ground check as well a stamina driven sprint. Just like that, tic-tac-toe, a winner.

## Custom Comparers
Fish-Networking generates comparers for prediction data to perform internal optimizations, but on occasion certain types cannot have comparers automatically generated.

You may see an error in the console about a type requiring a custom comparer. For example, generics and arrays specifically must have a custom comparer provided.

```csharp
/* For example, this will create an error stating
* byte[] needs a custom comparer. */
  public struct MoveData : IReplicateData
  {
  public Vector2 MoveDirection;
  public byte[] CustomData;
  //rest omitted..
  }

```
  While a comparer could be made automatically for the type byte[], we still require you to create your own as we may not know exactly how you want to compare these types. The code below compares byte arrays by iterating every byte to check for mismatches. Given how often prediction data sends, this could potentially burden the processor.

```csharp
[CustomComparer]
public static bool CompareByteArray(byte[] a, byte[] b)
{
bool aNull = (a is null);
bool bNull = (b is null);
//Both are null.
if (aNull && bNull) return true;
//One is null, other is not.
if (aNull != bNull) return false;
//Not same lengths, cannot match.
if (a.Length != b.Length) return false;

//Both not null and same length, compare bytes.
int length = a.Length;
for (int i = 0; i < length; i++)
{
//Differs.
if (a[i] != b[i]) return false;
}

//Fall through, if here everything matches.
return true;
}

```
The above code is a working example of how to create a custom comparer, but it may not be the most ideal comparer for your needs; this is why we require you to make your own comparer for such types.

Creating your own comparer is simple. Make a new static method with any name and boolean as the return type. Decorate the method with the [CustomComparer] attribute. There must also be two parameters, each being the type you want to compare. The method logic can contain whichever code you like.

## PredictionRigidbody
This class provides accurate simulations and re-simulations when applying outside forces, most commonly through collisions.

Using PredictionRigidbody is very straight forward. In short, you move from applying force changes to the rigidbody onto the PredictionRigidbody instance instead.

View the Creating Code guide for using PredictionRigidbody in your replicate and reconcile methods.

Using Outside The Script
As mentioned above you should always apply forces to the PredictionRigidbody component rather than the Rigidbody directly. Our first guides demonstrate how to do this within the replicate method, as well how to reconcile using the PredictionRigidbody, but do not show how to add forces from outside scripts, such as a bumper in your game.

There is virtually no complexity to adding outside forces other than remembering to add them again, to the PredictionRigidbody and not the Rigidbody.

The example below is what it might look like if using a trigger on a world object to repel the player.

For triggers and collisions to work properly with prediction you must use our NetworkTrigger/NetworkCollision components. Otherwise, due to a Unity limitation, such interactions would not work.  You can learn more about those components here.

Fun fact: Fish-Networking is the only framework that has the ability to simulate Enter/Exit events with prediction; not even Fusion does this!~~~~

```csharp
private void NetworkTrigger_OnEnter(Collider other)
{
//Add upward impulse when hitting this trigger.
if (other.TryGetComponent<RigidbodyPlayer>(out rbPlayer))
rbPlayer.PredictionRigidbody.AddForce(Vector3.up, ForceMode.Impulse);
//Do not call Simulate on the PredictionRigidbody here. That should only be done
//within the replicate method.
}

```
There is a fair chance a number of controllers will want to set velocities directly as well. PredictionRigidbody does support this. We encourage you to review the PredictionRigidbody API for all available functionality.

Our last example demonstrates setting velocities directly.

You would still call PredictionRigidbody.Simulate() regardless of how velocities are set.

```csharp
float horizontal = data.Horizontal;
Vector3 velocity = new Vector3(data.Horizontal, 0f, 0f) * _moveRate;
PredictionRigidbody.Velocity(velocity);

```