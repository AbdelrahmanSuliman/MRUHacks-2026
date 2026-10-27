using UnityEngine;
namespace CalgaryCycling
{
    public sealed class BicycleMotor : MonoBehaviour
    {
        public RoutePath Route { get; private set; }
        public float Distance { get; private set; }
        public float LateralOffset { get; private set; }
        public float Speed { get; private set; }
        public float Progress => Route == null ? 0 : Distance/Route.Length;
        public bool Arrived => Route != null && Distance >= Route.Length-.001f;
        RideSettings settings;
        float steer, steerVelocity;
        public void Initialize(RoutePath route, RideSettings config) { Route=route;settings=config;ResetRide(); }
        public void ResetRide()
        {
            Distance=LateralOffset=Speed=steer=steerVelocity=0;
            if(Route==null)return;
            transform.localPosition=Route.Position(0)+Vector3.up*settings.stylizedGroundOffset;
            transform.localRotation=Quaternion.LookRotation(Route.Forward(0),Vector3.up);
        }
        public void Stop() { Speed=0; steer=steerVelocity=0; }
        public void Tick(float input, float deltaTime)
        {
            if(Route==null || Arrived)return;
            float dt=Mathf.Min(deltaTime,.05f); // avoid teleportation on a long frame
            float remaining=Route.Length-Distance;
            float target=Mathf.Min(settings.cruisingSpeed,Mathf.Sqrt(2*settings.acceleration*remaining));
            Speed=Mathf.MoveTowards(Speed,target,settings.acceleration*dt);
            steer=Mathf.SmoothDamp(steer,Mathf.Clamp(input*settings.steeringSensitivity,-1,1),
                ref steerVelocity,settings.steeringSmoothTime,Mathf.Infinity,dt);
            float angle=steer*settings.maxSteeringAngle*Mathf.Deg2Rad;
            Distance=Mathf.Min(Route.Length,Distance+Speed*Mathf.Cos(angle)*dt);
            // Steering changes actual world/geographic position, not just a handlebar animation.
            // Fade outward motion at the corridor edge. No gravity, collisions or accumulated yaw.
            float edge=1;
            if(Mathf.Sign(steer)==Mathf.Sign(LateralOffset))
                edge=Mathf.Clamp01((settings.corridorHalfWidth-Mathf.Abs(LateralOffset))/.35f);
            LateralOffset=Mathf.Clamp(LateralOffset+Speed*Mathf.Sin(angle)*dt*edge,
                -settings.corridorHalfWidth,settings.corridorHalfWidth);
            Vector3 old=transform.localPosition;
            Vector3 next=Route.Position(Distance,LateralOffset)+Vector3.up*settings.stylizedGroundOffset;
            Vector3 motion=next-old;motion.y=0;
            transform.localPosition=next;
            if(motion.sqrMagnitude>.0000001f)
                transform.localRotation=Quaternion.RotateTowards(transform.localRotation,
                    Quaternion.LookRotation(motion,Vector3.up),settings.maxYawDegreesPerSecond*dt);
            if(Route.Length-Distance<.02f) { Distance=Route.Length;Speed=0;transform.localPosition=Route.Position(Distance,LateralOffset)+Vector3.up*settings.stylizedGroundOffset; }
        }
    }
}
