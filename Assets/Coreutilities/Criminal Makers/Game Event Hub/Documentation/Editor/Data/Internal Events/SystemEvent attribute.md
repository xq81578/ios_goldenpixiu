# [SystemEvent] Attribute

The events from the previous section are flagged with the attribute `[SystemEvent]`.

This attribute is used to mark events that are part of the system and should not be used in the game. This attribute is used by the system to hide these events from the user in most of the tools.

Exclusions:

- `Monitor` tool hide these events

- `Tester` tool won't allow you to raise these events

- `GameEventHub` component Status won't count `Active Events` neither `Active Subscribers` for these events