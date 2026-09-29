# C# 참조 전달과 Unity GC 정리

이 문서는 Three Souls 프로젝트에서 객체 풀, `ICombatWorld`, `ICombatStateProvider`, `GetComponent<T>()`, 이벤트 구조체를 검토하며 확인한 C# 메모리와 GC 개념을 정리한다.

## 먼저 구분할 개념

- **메모리 사용:** 변수, 필드, 객체가 저장 공간을 사용하는 것
- **managed allocation:** C# 관리 힙에 새로운 객체가 생성되는 것
- **GC:** 관리 힙에 생성됐지만 더 이상 사용하지 않는 객체를 수거하는 작업
- **CPU 비용:** 탐색, 정렬, 복사, 간접 호출 등을 실행하는 시간

메모리를 조금 더 사용한다는 사실이 곧 매 호출마다 GC를 발생시킨다는 의미는 아니다. 기존 객체를 가리키는 참조 필드 하나를 추가하는 것과 새로운 객체를 반복 생성하는 것은 구분해야 한다.

## 참조형 객체를 전달할 때

`class`, `string`, `MonoBehaviour`는 참조형이다. 참조형 변수를 다른 변수에 대입하면 객체 전체가 복사되지 않고 동일한 객체를 가리키는 참조가 복사된다.

```csharp
GameCombatManager manager = GameCombatManager.Get();
ICombatWorld world = manager;
```

```text
manager ──┐
          ├──> 기존 GameCombatManager 객체
world ────┘
```

`GameCombatManager`의 크기를 가정상 30바이트라고 해도 `world`에 30바이트 전체가 복사되는 것은 아니다. 실제 객체는 하나이며 `world`에는 그 객체를 가리키는 참조만 저장된다.

64비트 환경에서 참조 하나는 일반적으로 8바이트다. 다만 플랫폼, 런타임, 정렬 및 최적화에 따라 실제 배치는 달라질 수 있으므로 항상 정확히 8바이트가 추가된다고 단정하지 않는다.

### 매개변수와 필드의 차이

```csharp
private ICombatWorld CombatWorld;

public void Initialize(ICombatWorld world)
{
    CombatWorld = world;
}
```

- `world` 매개변수는 호출 중 전달되는 참조다. 실제로는 레지스터나 스택을 사용하거나 최적화될 수 있다.
- `CombatWorld` 필드는 객체가 살아 있는 동안 참조 하나를 보관한다.
- 어느 경우에도 원래 `GameCombatManager` 객체 전체를 복제하지 않는다.
- 이 대입만으로 새로운 관리 객체가 만들어지지 않으므로 반복적인 GC 원인이 되지 않는다.

## 문자열 대입

`string`도 참조형이며 불변 객체다.

```csharp
string a = "Hello";
string b = a;
```

`a`와 `b`는 동일한 문자열 객체를 가리킨다. `b = a`에서는 문자열 내용이 복사되거나 새로운 문자열 객체가 생성되지 않는다.

```text
a ──┐
    ├──> string 객체 "Hello"
b ──┘
```

반면 문자열을 가공하면 일반적으로 새로운 문자열이 생성된다.

```csharp
b = a + " World";
b = a.ToUpper();
b = a.Substring(1);
```

반복문이나 매 프레임 실행 경로에서 문자열 보간, 연결 및 변환을 수행하면 managed allocation과 이후 GC의 원인이 될 수 있다.

## 인터페이스 참조와 boxing

클래스 객체를 인터페이스로 참조하는 것은 객체 복제가 아니다.

```csharp
public class GameCombatManager : MonoBehaviour, ICombatWorld
{
}

ICombatWorld world = GameCombatManager.Get();
```

`GameCombatManager`는 클래스이므로 `world`에는 기존 객체를 가리키는 참조만 저장된다. 인터페이스 호출에는 구체 타입 직접 호출보다 약간의 간접 호출 비용이 있을 수 있지만, 이 대입 때문에 관리 객체가 새로 생성되지는 않는다.

값 타입인 `struct`를 인터페이스 또는 `object`로 변환하면 boxing이 발생할 수 있다.

```csharp
public struct CombatWorldValue : ICombatWorld
{
}

ICombatWorld world = new CombatWorldValue(); // boxing 가능
```

boxing된 값은 관리 힙 객체가 되므로 GC 대상이 된다.

## `ICombatWorld`와 `ICombatStateProvider`

상태에 두 인터페이스를 전달해도 실제 구현이 클래스라면 매 상태 업데이트마다 새로운 객체가 생성되지 않는다.

```csharp
public sealed class CombatAttackState : ICombatState
{
    private readonly ICombatWorld CombatWorld;

    public CombatAttackState(ICombatWorld combatWorld)
    {
        CombatWorld = combatWorld;
    }

    public void OnStateUpdate(ICombatStateProvider entity)
    {
        CombatWorld.IsTargetAlive(entity.EnemyTarget);
    }
}
```

- `CombatAttackState`를 `new` 할 때 상태 객체는 한 번 할당된다.
- `CombatWorld`에는 참조 하나가 저장된다.
- `entity` 매개변수로는 기존 `BaseEntity` 참조가 전달된다.
- 상태 업데이트 호출 자체에서는 새 객체가 필요하지 않다.

다음처럼 매 업데이트마다 클래스 컨텍스트를 생성하면 실제 할당이 발생한다.

```csharp
CurrentState.Update(new CombatStateContext(entity, combatWorld));
```

컨텍스트 객체가 필요하다면 초기화할 때 한 번 생성해 재사용하거나, 상태에 `ICombatWorld`를 한 번 주입하고 기존 `ICombatStateProvider` 매개변수를 그대로 사용하는 것이 적합하다.

## `GetComponent<T>()`와 GC

다음과 같은 제네릭 호출이 컴포넌트를 정상적으로 찾는 경우, 일반적으로 매 호출마다 새로운 관리 객체나 배열을 생성하지 않는다.

```csharp
var health = gameObject.GetComponent<EntityHealth>();
```

반환되는 것은 이미 존재하는 컴포넌트의 참조다. 주된 비용은 컴포넌트 탐색, Unity 네이티브 엔진과 C# 사이의 호출, 호출 빈도에 따른 CPU 시간 누적이다. 따라서 반복 호출을 피하는 이유는 보통 GC보다 탐색 비용 때문이다.

```csharp
private EntityHealth Health;

private void Awake()
{
    Health = GetComponent<EntityHealth>();
}
```

주의할 예외도 있다.

- 컴포넌트를 찾지 못한 `GetComponent<T>()`는 Unity Editor에서 할당이 발생할 수 있다. 존재 여부를 검사하는 용도라면 `TryGetComponent<T>()`를 사용한다.
- 배열을 반환하는 `GetComponents<T>()`는 결과 배열을 새로 생성한다.
- `GetComponents(List<T> results)`는 기존 리스트를 재사용할 수 있다.
- 문자열 기반 `GetComponent("TypeName")`보다 제네릭 버전을 우선한다.

공식 문서:

- [Unity Component.GetComponent](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Component.GetComponent.html)
- [Unity Component.TryGetComponent](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Component.TryGetComponent.html)
- [Unity Component.GetComponents](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Component.GetComponents.html)

현재 `ObjectPoolExtensions.SpawnByOrigin<T>()`에서는 풀의 제네릭 메서드가 이미 `GetComponent<T>()`를 수행한 결과에 다시 `GetComponent<T>()`를 호출하는 구조가 확인됐다. 이는 반드시 GC를 생성하는 문제는 아니지만 불필요한 컴포넌트 탐색이다. 타입을 보존하는 풀을 사용하면 스폰 시 조회 자체를 제거할 수 있다.

## 이벤트 구조체와 GC

`EventData.cs`의 이벤트는 값 타입이다.

```csharp
public struct Event_CharacterDie : IEventValue
{
    public readonly int InstanceID;
}
```

구조체가 인터페이스를 구현한다는 사실만으로 boxing이 발생하지는 않는다. 현재 `TriggerEvent`처럼 구체 타입을 제네릭 매개변수로 유지하면 이벤트 값을 boxing하지 않고 전달할 수 있다.

```csharp
public static void TriggerEvent<TSender, TEvent>(
    TSender sender,
    TEvent eventValue)
    where TSender : class
    where TEvent : IEventValue
{
}

// TEvent가 Event_CharacterDie로 유지되므로 boxing 없음
EventUtility.TriggerEvent(this, new Event_CharacterDie(10));
```

다음과 같이 실제 인터페이스 또는 `object` 변수에 담으면 boxing된다.

```csharp
IEventValue eventValue = new Event_CharacterDie(10);
object boxedEvent = new Event_CharacterDie(10);
```

현재 이벤트 전달에서 더 직접적인 GC 원인은 이벤트 구조체가 아니라 다음 LINQ와 리스트 복사다.

```csharp
var list = EventListenerContainer[eventType]
    .OrderBy(x => ((IEventListener<TEvent>)x).CallbackOrder)
    .ToList();
```

`OrderBy` 정렬 과정과 `ToList()` 결과 리스트는 이벤트가 발생할 때마다 새로운 관리 객체 및 버퍼를 만들 수 있다. 리스너 순서를 등록 시점에 정렬하고 발생 시 원본 리스트를 직접 순회하면 반복 할당을 줄일 수 있다.

단, 이벤트 콜백 도중 리스너가 등록 또는 해제될 수 있다면 원본 리스트 직접 순회는 안전하지 않을 수 있다. 이 경우에는 발행 중 변경을 보류하거나 이벤트 타입별로 재사용 가능한 스냅샷 버퍼를 보관해야 한다.

`IEventListenerBase` 목록에 현재처럼 `MonoBehaviour` 클래스 리스너를 저장하는 것은 boxing을 만들지 않는다. 구조체 리스너를 인터페이스 목록에 저장할 때만 boxing을 주의한다.

## 현재 코드에서 우선 확인할 할당 지점

다음 패턴은 `GetComponent<T>()`보다 먼저 확인할 가치가 있다.

```csharp
source.FindAll(predicate);
source.Where(predicate).ToList();
source.Select(selector).ToArray();
stringValue + anotherValue;
new SomeReferenceType();
```

- `FindAll`은 새로운 리스트를 반환한다.
- `ToList`는 새로운 리스트와 내부 배열을 만든다.
- `ToArray`는 새로운 배열을 만든다.
- LINQ 정렬은 정렬을 위한 객체와 버퍼를 사용할 수 있다.
- 문자열 연결과 보간은 새로운 문자열을 만들 수 있다.
- `List.RemoveAt(0)`은 일반적으로 GC를 만들지는 않지만 뒤의 요소를 모두 이동시키므로 풀의 대기 컬렉션에는 `Stack<T>` 또는 `Queue<T>`가 더 적합하다.

## 판단 기준

메모리 최적화 시 다음 순서로 판단한다.

1. 새로운 클래스, 배열, 리스트, 문자열 또는 boxing 객체가 생성되는가?
2. 해당 생성이 초기화 시 한 번인가, 매 프레임 또는 이벤트마다 반복되는가?
3. 참조만 복사하는가, 실제 데이터를 복사하는가?
4. 할당은 없지만 탐색, 정렬 또는 전체 요소 이동 같은 CPU 비용이 있는가?
5. Unity Profiler의 `GC Alloc`과 CPU 시간을 실제 실행 경로에서 확인했는가?

참조 필드 몇 개를 추가해 의존성을 인터페이스로 분리하는 비용은 일반적으로 매우 작다. 반면 매 프레임 생성되는 리스트, 배열, 문자열 및 boxing은 누적되어 GC 스파이크를 만들 수 있으므로 반복 빈도와 수명을 기준으로 우선순위를 정한다.
