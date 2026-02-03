using System.Collections.Generic;
using UnityEngine;
public class CustomerQueueManager : MonoBehaviour
{
    [Header("����� ������������")]
    public Transform[] servicePoints; // ��������� ������

    [Header("�������")]
    private Queue<ZombieCustomer> zombieQueue = new Queue<ZombieCustomer>();
    private Dictionary<Transform, ZombieCustomer> occupiedPoints = new Dictionary<Transform, ZombieCustomer>();

    public bool HasAvailableSpot()
    {
        return GetAvailableServicePoint() != null;
    }

    public Transform GetAvailableServicePoint()
    {
        if (servicePoints == null || servicePoints.Length == 0)
            return null;

        foreach (var point in servicePoints)
        {
            if (point == null) continue;
            if (!occupiedPoints.ContainsKey(point))
                return point;
        }
        return null;
    }

    public void AddToQueue(ZombieCustomer zombie)
    {
        if (zombie == null) return;

        zombieQueue.Enqueue(zombie);

        // Если точки очереди не настроены — не падаем, просто оставляем в очереди
        if (servicePoints == null || servicePoints.Length == 0)
        {
            Debug.LogWarning("CustomerQueueManager: servicePoints не настроены — зомби не будет поставлен в очередь");
            return;
        }

        AssignZombieToSpot();
    }

    void AssignZombieToSpot()
    {
        if (zombieQueue.Count > 0)
        {
            Transform spot = GetAvailableServicePoint();
            if (spot != null)
            {
                ZombieCustomer zombie = zombieQueue.Dequeue();
                occupiedPoints[spot] = zombie;
                zombie.servicePoint = spot;
                zombie.GoToServicePoint();
            }
        }
    }

    public void RemoveZombie(ZombieCustomer zombie)
    {
        // Удаляем зомби из занятой точки (если он ещё числится в очереди)
        foreach (var kvp in occupiedPoints)
        {
            if (kvp.Value == zombie)
            {
                occupiedPoints.Remove(kvp.Key);
                break;
            }
        }

        // Освободившееся место заполняем новым зомби из очереди (если есть)
        AssignZombieToSpot();
    }

    // Вызывать в момент, когда ПЕРВЫЙ зомби физически уходит с QueuePoint1
    public void OnFrontZombieLeftPoint(ZombieCustomer zombie)
    {
        if (zombie == null) return;
        if (servicePoints == null || servicePoints.Length == 0) return;
        if (servicePoints[0] == null) return;

        // Если это действительно первый — освобождаем точку и двигаем очередь
        if (occupiedPoints.TryGetValue(servicePoints[0], out ZombieCustomer front) && front == zombie)
        {
            occupiedPoints.Remove(servicePoints[0]);
            ShiftQueueForward();
            AssignZombieToSpot();
        }
    }

    // Передняя точка очереди (QueuePoint1) — это servicePoints[0]
    public bool IsFrontZombie(ZombieCustomer zombie)
    {
        if (zombie == null) return false;
        if (servicePoints == null || servicePoints.Length == 0) return false;
        if (servicePoints[0] == null) return false;

        return occupiedPoints.TryGetValue(servicePoints[0], out ZombieCustomer front) && front == zombie;
    }

    void ShiftQueueForward()
    {
        if (servicePoints == null || servicePoints.Length == 0) return;

        for (int i = 0; i < servicePoints.Length - 1; i++)
        {
            Transform current = servicePoints[i];
            Transform next = servicePoints[i + 1];

            if (current == null || next == null) continue;

            bool currentOccupied = occupiedPoints.ContainsKey(current);
            if (currentOccupied) continue;

            if (occupiedPoints.TryGetValue(next, out ZombieCustomer zombie) && zombie != null)
            {
                occupiedPoints.Remove(next);
                occupiedPoints[current] = zombie;

                zombie.servicePoint = current;
                zombie.GoToServicePoint();
            }
        }
    }

    // Зомби, который сейчас обслуживается (первый в очереди) и реально ЖДЕТ вещь
    public ZombieCustomer GetFirstWaitingZombie()
    {
        if (servicePoints == null) return null;

        foreach (var point in servicePoints)
        {
            if (point == null) continue;
            if (occupiedPoints.TryGetValue(point, out ZombieCustomer zombie) && zombie != null)
            {
                if (zombie.currentState == ZombieCustomer.ZombieState.Waiting ||
                    zombie.currentState == ZombieCustomer.ZombieState.GettingAngry)
                    return zombie;
            }
        }

        return null;
    }
}