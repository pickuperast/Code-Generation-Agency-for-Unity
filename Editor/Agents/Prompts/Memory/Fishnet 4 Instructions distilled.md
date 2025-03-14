Okay, here's a refined version of the provided guidelines, focusing on clarity, conciseness, and a logical flow for users wanting to integrate SyncTypes into their Fish-Networking projects. I've also made some assumptions and improvements based on common use cases and best practices.

**# Understanding and Customizing SyncTypes**

SyncTypes in Fish-Networking provide a powerful and flexible way to automatically synchronize data across your networked game. This guide explains how to customize their behavior and integrate them effectively.

## **1. SyncType Settings**

Every SyncType allows you to configure its behavior using `SyncTypeSettings`. You can set these during initialization or update them at runtime.

```csharp
using FishNet.Transporting; // Required for Channel.Unreliable

//Default settings on initialize
private readonly SyncVar<int> _health = new SyncVar<int>();

//Custom settings on initialize
private readonly SyncVar<int> _ammo = new SyncVar<int>(new SyncTypeSettings(0.1f, Channel.Unreliable));

private void Awake()
{
    // Update all settings at once:
    _health.UpdateSettings(new SyncTypeSettings(0.5f, Channel.Reliable));

    // Update only the send rate:
    _health.UpdateSendRate(0.5f); // Send updates at most twice per second
}
```

**Key Takeaways:**

*   `SyncTypeSettings` control synchronization frequency, network channel, and more.
*   Initialize settings directly or modify them later using `UpdateSettings` or specific update methods (e.g., `UpdateSendRate`).
*   Experiment to find the balance between synchronization accuracy and bandwidth usage. Use unreliable carefully.

## **2. Displaying SyncTypes in the Inspector**

To view and edit SyncTypes directly in the Unity Inspector:

1.  **Make Containers Serializable:** If your SyncType holds a custom data type, mark that type as `[System.Serializable]`.
2.  **Allow Mutability:** Add the `[AllowMutableSyncType]` attribute above the SyncType declaration.
3.  **Serialize the Field:** Add the `[SerializeField]` attribute to the SyncType field (if it's not public).
4.  **Remove readonly indicator:** The SyncType must not use the 'readonly' indicator.

```csharp
[System.Serializable]
public struct MyDataType {
    public int Value;
}

[AllowMutableSyncType]
[SerializeField]
private SyncVar<MyDataType> _myData = new SyncVar<MyDataType>();
```

**Important Considerations:**

*   **Avoid Runtime Initialization:** NEVER assign a *new* SyncType to a SyncType variable at runtime. The proper way to change the value of a SyncType is to modify the underlying data it represents.
*   The `readonly` keyword is omitted when you want to show the data in the inspector. The removal of the `readonly` keyword requires adding the `[AllowMutableSyncType]` attribute above the SyncType to prevent compilation errors in the editor.

## **3. Working with Specific SyncTypes**

### **3.1 SyncVar: Simple Variable Synchronization**

`SyncVar` is the simplest SyncType, used to synchronize single variables.

```csharp
public class MyClass : NetworkBehaviour
{
    private readonly SyncVar<float> _health = new SyncVar<float>(new SyncTypeSettings(0.25f, Channel.Reliable)); // Send at most 4 times per second

    private void Awake()
    {
        _health.OnChange += OnHealthChanged;
    }

    private void OnHealthChanged(float previousValue, float newValue, bool asServer)
    {
        Debug.Log($"Health changed from {previousValue} to {newValue} on {(asServer ? "server" : "client")}.");
        // Update UI or perform other actions
    }

    [ServerRpc]
    public void CmdChangeHealth(float newHealth)
    {
        _health.Value = newHealth;
    }

        private void OnDisable()
        {
            _health.OnChange -= OnHealthChanged;
        }
}
```

**Key Points:**

*   Changes to `_health.Value` on the server will automatically be sent to clients.
*   The `OnChange` event provides notifications of changes on both the server and the client.
*   Consider throttling client-side SyncVar changes to reduce bandwidth.

#### **Client-Side Authority with SyncVars**

To allow clients to *request* changes to SyncVars:

1.  Create a `ServerRpc` to handle the request.
2.  Consider using `WritePermission.ClientUnsynchronized` for the `SyncTypeSettings` to allow the client to immediately update the value locally *before* the server confirms.
3.  Use `ReadPermission.ExcludeOwner` to prevent the owner from receiving their own update from the server (if `ClientUnsynchronized` is used).
4.  Use `RunLocally = true` in the ServerRpc to execute the RPC code on both the sender(client) and the server.

```csharp
private readonly SyncVar<string> _name = new SyncVar<string>(new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner));

[ServerRpc(RunLocally = true)]
private void CmdSetName(string newName)
{
    _name.Value = newName;
}

public void SetName(string name)
{
    CmdSetName(name);
}
```

### **3.2 SyncList: Synchronized Collections**

`SyncList` automatically synchronizes `List` collections across the network.

```csharp
private readonly SyncList<int> _myCollection = new SyncList<int>();

private void Awake()
{
    _myCollection.OnChange += OnMyCollectionChanged;
}

private void Update()
{
    if (IsServer)
    {
        _myCollection.Add(Random.Range(1, 101));
    }
}

private void OnMyCollectionChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
{
    switch (op)
    {
        case SyncListOperation.Add:
            Debug.Log($"Item {newItem} added at index {index} on {(asServer ? "server" : "client")}.");
            break;
        case SyncListOperation.RemoveAt:
            Debug.Log($"Item {oldItem} removed from index {index} on {(asServer ? "server" : "client")}.");
            break;
        // Handle other operations (Insert, Set, Clear, Complete)
    }
}
    private void OnDisable()
    {
        _myCollection.OnChange -= OnMyCollectionChanged;
    }
```

**Key Considerations:**

*   The `OnChange` event provides detailed information about list modifications.
*   For complex types *within* a `SyncList`, you may need to mark the list element as dirty, to ensure the client receives updated information.

*   For classes call `_myCollection.Dirty(index);`
*   For structs, create a local copy, modify it, and reassign it to the list `_myCollection[index] = modifiedStruct;`

   ```csharp
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

### **3.3 SyncHashSet**

`SyncHashSet` synchronizes HashSet collections across the network.

```csharp
private readonly SyncHashSet<int> _myCollection = new SyncHashSet<int>();

private void Awake()
{
    _myCollection.OnChange += OnMyCollectionChanged;
}

private void Update()
{
    if (IsServer)
    {
        _myCollection.Add(Random.Range(1, 101));
    }
}

private void OnMyCollectionChanged(SyncHashSetOperation op, int item, bool asServer)
{
    switch (op)
    {
        case SyncHashSetOperation.Add:
            Debug.Log($"Item {item} added on {(asServer ? "server" : "client")}.");
            break;
        case SyncHashSetOperation.Remove:
            Debug.Log($"Item {item} removed on {(asServer ? "server" : "client")}.");
            break;
        case SyncHashSetOperation.Clear:
            Debug.Log($"Collection cleared {(asServer ? "server" : "client")}.");
            break;
        case SyncHashSetOperation.Update:
            Debug.Log($"Collection updated with {item} {(asServer ? "server" : "client")}.");
            break;
        // Handle other operations (Insert, Set, Complete)
    }
}
private void OnDisable()
{
    _myCollection.OnChange -= OnMyCollectionChanged;
}
```

**Key Considerations:**

*   The `OnChange` event provides detailed information about list modifications.

### **3.4 SyncDictionary**

`SyncDictionary` synchronizes Dictionaries across the network.

```csharp
private readonly SyncDictionary<int, string> _playerNames = new SyncDictionary<int, string>();

private void Awake()
{
    _playerNames.OnChange += OnPlayerNamesChanged;
}

private void Update()
{
    if (IsServer)
    {
        _playerNames[Random.Range(1, 101)] = "Player" + Random.Range(1, 101);
    }
}

private void OnPlayerNamesChanged(SyncDictionaryOperation op, int key, string value, bool asServer)
{
    switch (op)
    {
        case SyncDictionaryOperation.Add:
            Debug.Log($"Player {key} added with name {value} on {(asServer ? "server" : "client")}.");
            break;
        case SyncDictionaryOperation.Remove:
            Debug.Log($"Player {key} removed on {(asServer ? "server" : "client")}.");
            break;
        case SyncDictionaryOperation.Set:
            Debug.Log($"Player {key} set to {value} on {(asServer ? "server" : "client")}.");
            break;
        case SyncDictionaryOperation.Clear:
            Debug.Log($"Collection cleared {(asServer ? "server" : "client")}.");
            break;
        case SyncDictionaryOperation.Complete:
            Debug.Log($"All callbacks have completed for this tick {(asServer ? "server" : "client")}.");
            break;
    }
}

private void OnDisable()
{
    _playerNames.OnChange -= OnPlayerNamesChanged;
}
```

**Key Considerations:**

*   The `OnChange` event provides detailed information about list modifications.
*   For complex types *within* a `SyncDictionary`, you may need to mark the list element as dirty, to ensure the client receives updated information.

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

### 3.5 **SyncTimer:** Synchronized Timers

`SyncTimer` lets you synchronize timer events between server and clients.

```csharp
private readonly SyncTimer _timeRemaining = new SyncTimer();

private void Awake()
{
    _timeRemaining.OnChange += OnTimeRemainingChanged;
}

private void Update()
{
    if(IsServer)
    {
        if (!_timeRemaining.IsActive)
        {
            _timeRemaining.StartTimer(5f);
        }
    }

    _timeRemaining.Update(Time.deltaTime);
}

private void OnTimeRemainingChanged(SyncTimerOperation op, float prev, float next, bool asServer)
{
    switch (op)
    {
        case SyncTimerOperation.Start:
            Debug.Log($"Timer started with {next} seconds on {(asServer ? "server" : "client")}.");
            break;
        case SyncTimerOperation.Pause:
            Debug.Log($"Timer paused on {(asServer ? "server" : "client")}.");
            break;
        case SyncTimerOperation.Finished:
            Debug.Log($"Timer finished on {(asServer ? "server" : "client")}.");
            break;
    }
}
private void OnDisable()
{
    _timeRemaining.OnChange -= OnTimeRemainingChanged;
}
```

**Key Points:**

*   Use `_timeRemaining.StartTimer()`, `_timeRemaining.PauseTimer()`, `_timeRemaining.UnpauseTimer()`, and `_timeRemaining.StopTimer()` to control the timer on the server.
*   Call `_timeRemaining.Update()` in your `Update()` loop on both the server and clients.
*   Access the current time remaining with `_timeRemaining.Remaining`.
*   Subscribe to the `OnChange` event to be notified of timer state changes.

### 3.6 **SyncStopwatch:** Synchronized Stopwatch

`SyncStopwatch` lets you synchronize stopwatch events between server and clients.

```csharp
private readonly SyncStopwatch _timePassed = new SyncStopwatch();

private void Awake()
{
    _timePassed.OnChange += OnTimePassedChanged;
}

private void Update()
{
    if(IsServer)
    {
        if (!_timePassed.IsRunning)
        {
            _timePassed.StartStopwatch();
        }
    }

    _timePassed.Update(Time.deltaTime);
}

private void OnTimePassedChanged(SyncStopwatchOperation op, float prev, bool asServer)
{
    switch (op)
    {
        case SyncStopwatchOperation.Start:
            Debug.Log($"Stopwatch started on {(asServer ? "server" : "client")}.");
            break;
        case SyncStopwatchOperation.Pause:
            Debug.Log($"Stopwatch paused on {(asServer ? "server" : "client")}.");
            break;
        case SyncStopwatchOperation.Stop:
            Debug.Log($"Stopwatch stopped on {(asServer ? "server" : "client")}.");
            break;
    }
}

private void OnDisable()
{
    _timePassed.OnChange -= OnTimePassedChanged;
}
```

**Key Points:**

*   Use `_timePassed.StartStopwatch()`, `_timePassed.PauseStopwatch()`, and `_timePassed.StopStopwatch()` to control the timer on the server.
*   Call `_timePassed.Update(Time.deltaTime)` in your `Update()` loop on both the server and clients.
*   Access the current time passed with `_timePassed.Elapsed`.
*   Subscribe to the `OnChange` event to be notified of timer state changes.

### **4. Custom SyncTypes**

For ultimate control over synchronization, create your own `SyncType`s.

1.  Create a class that inherits from `SyncBase` and implements `ICustomSync`.
2.  Implement `GetSerializedType()` to specify the type that should be serialized, or `null` if you'll handle serialization manually.
3.  Implement your own write and read methods for handling specific serialization tasks in the type.

```csharp
[System.Serializable]
public struct MyContainer
{
    public int LeftArmHealth;
    public int RightArmHealth;
    public int LeftLegHealth;
    public int RightLegHealth;
}

public class SyncMyContainer : SyncBase, ICustomSync
{
    public object GetSerializedType() => typeof(MyContainer);
}

public class YourClass
{
    private readonly SyncMyContainer _myContainer = new();
}
```

**Important Note:** Refer to the Fish-Networking examples for complete custom `SyncType` implementations.

## **5. Broadcasts: Sending Messages Without NetworkObjects**

Broadcasts allow you to send messages to one or more objects without requiring a NetworkObject component. This is useful for communicating between objects which are not necessarily networked, such as a chat system.

```csharp
using FishNet;
using FishNet.Broadcast;
using FishNet.Transporting;
using UnityEngine;

public struct ChatBroadcast : IBroadcast
{
    public string Username;
    public string Message;
    public Color FontColor;
}

public class ChatManager : MonoBehaviour
{
    private void OnEnable()
    {
        InstanceFinder.ClientManager.RegisterBroadcast<ChatBroadcast>(OnChatBroadcastClient);
        InstanceFinder.ServerManager.RegisterBroadcast<ChatBroadcast>(OnChatBroadcastServer);
    }

    private void OnDisable()
    {
        InstanceFinder.ClientManager.UnregisterBroadcast<ChatBroadcast>(OnChatBroadcastClient);
        InstanceFinder.ServerManager.UnregisterBroadcast<ChatBroadcast>(OnChatBroadcastServer);
    }

    // Client sends message to server
    public void SendChatMessage(string message)
    {
        ChatBroadcast msg = new ChatBroadcast()
        {
            Message = message,
            FontColor = Color.white
        };

        InstanceFinder.ClientManager.Broadcast(msg);
    }

    // Server receives message and relays to all observers of sender
    private void OnChatBroadcastServer(NetworkConnection conn, ChatBroadcast msg, Channel channel)
    {
        // For simplicity, assume username is derived from connection.
        msg.Username = "User" + conn.ClientId;

        if (conn.FirstObject != null)
        {
            InstanceFinder.ServerManager.Broadcast(conn.FirstObject, msg, true);
        }
    }

    // Client receives message from server
    private void OnChatBroadcastClient(ChatBroadcast msg, Channel channel)
    {
        Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(msg.FontColor)}>{msg.Username}: {msg.Message}</color>");
    }
}
```

**Key Points:**

*   Broadcasts are structs that implement `IBroadcast`.
*   Send broadcasts using `ClientManager.Broadcast()` (client to server) and `ServerManager.Broadcast()` (server to clients).
*   Register to receive broadcasts using `ClientManager.RegisterBroadcast()` and `ServerManager.RegisterBroadcast()`.
*   Remember to unregister broadcasts in `OnDisable()` to prevent memory leaks.

## **6. Observers: Controlling Visibility**

Observers are clients that can see and interact with a NetworkObject. Fish-Networking's Observer system allows fine-grained control over which clients observe which objects.

**Core Components:**

*   **NetworkObserver:** Component on NetworkObjects that manages observer conditions.
*   **ObserverManager:** Global manager for observer rules and conditions.
*   **ObserverCondition:** Scriptable objects that define rules for observer visibility.

**Common Workflow:**

1.  Add a `NetworkObserver` component to your NetworkObject.
2.  Add `ObserverCondition` assets (e.g., `DistanceCondition`, `OwnerOnlyCondition`) to the NetworkObserver's list of conditions.
3.  Modify condition properties at runtime to change visibility rules.

**Example: Modifying Distance Condition**

```csharp
using FishNet;
using FishNet.Component.Observing;
using UnityEngine;

public class MyNetworkObject : NetworkBehaviour
{
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Modify the maximum distance for the DistanceCondition
        DistanceCondition distanceCondition = base.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
        if (distanceCondition != null)
        {
            distanceCondition.MaximumDistance = 25f; // Set the maximum distance to 25 units
        }
    }
}
```

**Creating Custom Conditions:**

1.  Create a new script that inherits from `ObserverCondition`.
2.  Override the `ConditionMet()` method to implement your visibility logic.
3.  Override `GetConditionType()` to specify the condition type (e.g., `ObserverConditionType.Normal`, `ObserverConditionType.Timed`).
4.  Create an asset menu to create instances of your condition.

## **7. Automatic and Custom Serializers**

Fish-Networking automatically generates serializers for types used in network communication (SyncVars, RPCs, Broadcasts, etc.). However, you can create custom serializers for specific types or to optimize serialization.

**Automatic Serialization:**

*   Fish-Networking automatically serializes all public and serialized fields within a type.
*   Use the `[System.NonSerialized]` attribute to exclude fields from serialization.

**Custom Serializers:**

1.  Create a static class with static methods for writing and reading your type.
2.  The write method must be named `WriteYourType(this Writer writer, YourType value)`.
3.  The read method must be named `ReadYourType(this Reader reader)`.
4.  Use the `Writer` and `Reader` classes to serialize and deserialize data.

**Example: Custom Vector2 Serializer**

```csharp
using FishNet.Serializing;
using UnityEngine;

public static class CustomSerializers
{
    public static void WriteVector2(this Writer writer, Vector2 value)
    {
        writer.WriteSingle(value.x);
        writer.WriteSingle(value.y);
    }

    public static Vector2 ReadVector2(this Reader reader)
    {
        return new Vector2()
        {
            x = reader.ReadSingle(),
            y = reader.ReadSingle()
        };
    }
}
```

**Global Custom Serializers:**

To use a custom serializer across all assemblies, add the `[UseGlobalCustomSerializer]` attribute to the type.

## 8. Interface Serializers
Interfaces are very commonly used in most Unity Projects. Since Interfaces are not classes, even if the interface only uses serializable fields, a custom serializer is still needed in order for SyncTypes, and RPCs to serialize them properly over the network.

In most cases you will want to interrogate an Interface as what its class type is, and serialize the entire types class over the network. This allows you to interrogate the interface later on the receiving client/server and have the data match what the sender has at the time it was sent as well.

If the Interface is a NetworkBehaviour you might as well send it as one because Serializing them over the network is only sending an ID for the receiving client to look up. Very Little network traffic, and you still get all of the data!

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

```csharp
public interface ISomething
{
string Name;
int Health;
ushort Level;
}
```
You still will have to use an Identifier to send what class the Interface is, but we will not be sending the entire class over the network. Just the Interface Properties.

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

## **9. Inheritance Serializers**

Another frequently asked question is how to handle serialization for classes which are inherited by multiple other classes. These are often used to allow RPCs to use the base class as a parameter, while permitting other inheriting types to be used as arguments. This approach is similar to Interface serializers.

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

## **10. Addressables**

Fish-Networking supports both addressable scenes and prefabs. To use addressables effectively, assign each addressables package a unique ushort Id (between 1 and 65535) and register the prefabs with the NetworkManager.

I removed the addressables example code for now to keep the guide size down, since this may be less common than the other features. I also did this so the guide would not be larger than what it was before the refactor.

**Important Resources:**

*   **Fish-Networking API Documentation:** For detailed information on all SyncTypes, settings, and methods.
*   **Fish-Networking Examples:** Examine the provided examples for practical implementations of SyncTypes and other features.

This comprehensive guide provides a strong foundation for understanding and utilizing SyncTypes in Fish-Networking. Remember to experiment with different settings and approaches to optimize your game's network performance and functionality.
