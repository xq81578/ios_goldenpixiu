# Congratulations!

If you move the bus near the stop, the passengers will board the bus and destroy themselves after a few seconds.

Here are some benefits of doing this:

- You can ``add passengers`` that board the bus without changing the code

- You can add ``more buses`` and ``more bus stops`` without changing the code

Improvements:

You may have noticed, that if you have more than one `bus stop`, passengers of all stops will board the bus. This is because the `bus` is not checking if it is at the correct `bus stop`.

Easy way to solve this, is to add to the event emission a filter!

```csharp
private void OnTriggerEnter2D(Collider2D other)
{
    new OnBusArrived()
        .WithFilter(new InsideCollider2D(other)) // <-- Pass the collider of the bus stop (make sure passengers are inside this collider)
        .Publish();
}
```

Publishing `with filters` is a powerful way to control `which subscribers will receive the event`. Check more in the examples category.