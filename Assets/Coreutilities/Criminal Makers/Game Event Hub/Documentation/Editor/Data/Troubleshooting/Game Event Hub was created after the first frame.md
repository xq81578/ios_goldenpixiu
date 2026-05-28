# Game Event Hub was created after the first frame

If you add the `Game Event Hub` prefab at runtime, you will receive this warning message:

> Game Event Hub was created after the first frame. Expect some subscriptions to be missed.

It's expected that the Game Event Hub is added to the scene and kept alive for the entire duration of the game. If you add the Game Event Hub at runtime, some subscriptions might be missed, because of race conditions.