# [Avalonia]: Hovered-event is not available yet
**Issue Number**: #2316
**URL**: https://api.github.com/repos/Mapsui/Mapsui/issues/2316
**Created by**: timunie

## Description
**The problem**
At the moment it is not easily possible to show a label on pointer over a pin (tooltip-like). If we had the hover event available, this would be easier to implement

**The proposed solution**
Implement an event that consumed the already available HoveredEventArgs

**Alternative solutions**
Let the users click on the label to show the info

**Additional context**
This applies to Avalonia only I believe. For WPF this event seems to be consumable. 


## Comments
### Comment 1 by pauldendulk
The hover event is problematic because you need to do a query on the data on every mouse move you do. This performs only in the simplest cases. I do not want to invest in that unless there is solution that performs properly. 

Then again it is possible to build something yourself, if you listen to the MouseMove event of the platform (Avalonia in your case) and query the data yourself, in code like this:

```csharp
protected async void OnMouseMove(MouseEventArgs e)
{
    if (e.Buttons == 1) return; 

    var mapInfo = this.mapControl.GetMapInfo(new MPoint(e.OffsetX, e.OffsetY));
    var id = (mapInfo?.Feature as MyFeatureType)?.Id;
    mapControl.Refresh();
}
```
And in SetCallout(Id) something like this:
```csharp
public bool SetCallout(int id)
{
    foreach (var f in features)
        f.CalloutEnabled = false;
    GetMyFeature(id).CalloutEnabled = true;
}
```



### Comment 2 by timunie
Hello @pauldendulk 

thank you for your fast and great response. The idea how to get the MapInfo was the missing part :-). My solution for performace reason is to use the native tooltip, this doesn't require to loop over each callback. 

### My solution: 

```cs
private void MapOnPointerMoved(object? sender, PointerEventArgs e)
{
    try
    {
        if (sender is MapControl mapControl)
        {
            var pos = e.GetPosition(mapControl); // used in Avalonia to get position relative to a cotnrol
            var info = MapControl.GetMapInfo(new MPoint(pos.X, pos.Y)); // get the MapInfo 

            if (info?.Feature?["name"] is { } name) // I only want to show features being named here
            {
                ToolTip.SetTip(mapControl, name);
                ToolTip.SetIsOpen(mapControl, true);
            }
            else
            {
                ToolTip.SetIsOpen(mapControl, false);
            }
        }
    }
    catch (Exception exception)
    {
        Log.Information(exception, "Not able to open the Tooltip for the given position");
    }
}
```

> [!TIP]
> If one wants to add a delay, a `DispatcherTimer` can be used. This should also increase performance for large data sets as the MapInfo is only queried if the pointer stops over an item for a while. 

-----

Do you want me to close this ticket? I am more than happy with the solution I have so far, so I let you decide :-)

### Comment 3 by pauldendulk
Thanks for the feedback @timunie. I will close the issue now, but you can close it also if your question is answered.

### Comment 4 by pauldendulk
I now read you answer properly and I have to add that the possible performance problem is in the MapControl.GetMapInfo call itself. Depending on which layers have IsMapInfoLayer = true and how many features are in those layers. Some kind of timer is indeed a good idea, even if it is just waiting 200 ms before a call to MapControl.GetMapInfo.

### Comment 5 by timunie
Upd: Here is the DispatcherTimer if anyone needs it. Please adjust it to your needs: 

```cs
    // -----  Map ToolTip ----------
    
    // Dispatcher Timer to not update Tooltip on every mouse move
    private DispatcherTimer? _toolTipUpdateTimer;
    // Remember current pointer position
    private Point _toolTipPosition;
    
    private void MapControl_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        // Setup the timer
        _toolTipUpdateTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(500), 
            DispatcherPriority.Normal,
            ToolTipTimerTick);
        
        // Start the timer
        _toolTipPosition = e.GetPosition(MapControl);
        _toolTipUpdateTimer.Start();
    }
    
    private void MapControl_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        // Re-Start the timer if needed
        
        _toolTipPosition = e.GetPosition(MapControl);
        _toolTipUpdateTimer?.Start();
    }
    
    private void MapControl_OnPointerExited(object? sender, PointerEventArgs e)
    {
        _toolTipUpdateTimer?.Stop();
    }

    private void ToolTipTimerTick(object? sender, EventArgs e)
    {
        try
        {
            var info = MapControl.GetMapInfo(new MPoint(_toolTipPosition.X, _toolTipPosition.Y));

            // That logic depends on your map design, so adjust it if needed
            if (info?.Feature?["name"] is { } name && info.Style is SymbolStyle)
            {
                ToolTip.SetTip(MapControl, name);
                ToolTip.SetIsOpen(MapControl, true);
            }
            else
            {
                ToolTip.SetIsOpen(MapControl, false);
            }
        }
        catch (Exception exception)
        {
            Log.Information(exception, "Not able to open the Tooltip for the given position");
        }
        
        // Stop DispatcherTimer once the function was called once.
        _toolTipUpdateTimer?.Stop();
    }
```

