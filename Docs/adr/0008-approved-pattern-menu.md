---
status: accepted
---

# A short menu of design patterns; Singleton and Service Locator are banned

To keep the code from drifting into a new structure every ticket, only six patterns (as defined on [refactoring.guru](https://refactoring.guru/design-patterns)) are approved, each tied to the place it is for: **State** for the Packing Sequence (Dobot) and the UNO conveyor/Bag Closer sequence; **Command** for one typed message per Quest → Hub command; **Adapter** around the Dobot SDK and the serial port; **Proxy** as the safety limiter every motion passes through (workspace bounds, minimum Z, speed caps, Engage (formerly the Deadman, [ADR-0026](0026-teleop-controls-engage-toggle-pump-hand-menus.md))); **Facade** as Unity's single networking class; **Observer** as C# events. Any other pattern needs a new ADR. The approved patterns apply at their named places from the first use; any *other* new abstraction waits for its third use (Rule of Three).

**Singleton and Service Locator are banned**, although Unity's own SOLID e-book teaches Singleton for small games and Fowler does not condemn Service Locator. Our reasons: tests replace the MG400 and the serial port with fakes, and a Singleton or Locator hides that dependency so it can't be swapped; and static state survives Unity scene reloads, which breeds bugs that look random. Dependencies arrive through `[SerializeField]` references or constructor parameters from one composition root instead. See [Docs/research/coding-standards.md](../research/coding-standards.md) §5. The team expects to revisit the ban if a concrete need appears that injection cannot meet.
