# Independent arithmetic model of longitudinal policy. NOT a Unity/C# test runner.
import math

def step(x, v, leader_x, leader_v, dt, cruise=9):
    gap=leader_x-x-4.5
    available=max(0,gap-2)
    target=min(cruise, max(0,math.sqrt(36+10*(available+leader_v**2/10))-6),available/1.2)
    v=max(target,v-5*dt) if target<v else min(target,v+3.5*dt)
    v=min(v,available/dt)
    return x+v*dt,v
for dt in [.02,.1]:
    x,v=0,0
    braked=False
    for i in range(round(30/dt)):
        old=v;x,v=step(x,v,50,0,dt)
        braked |= 1<v<old
        assert 50-x>=6.499999
    assert v<.05 and braked
    print(f'stationary dt={dt}: centre gap={50-x:.3f}, final speed={v:.5f}')
x,v,l,lv=0,0,25,0
for i in range(1500):
    lv=min(3,lv+3.5*.02);l+=lv*.02
    x,v=step(x,v,l,lv,.02)
    assert l-x>=6.4
assert abs(v-3)<.15
print(f'slow leader: centre gap={l-x:.3f}, follower speed={v:.3f}')
for reverse in [False,True]:
    xs=[50,25,0];vs=[0,0,0]
    for i in range(2000):
        for j in ([2,1,0] if reverse else [0,1,2]):
            if j==0:
                if i>=1000:vs[0]=min(9,vs[0]+3.5*.02);xs[0]+=vs[0]*.02
            else: xs[j],vs[j]=step(xs[j],vs[j],xs[j-1],vs[j-1],.02)
        assert xs[0]-xs[1]>=6.4 and xs[1]-xs[2]>=6.4
        if i==999:assert vs[2]<.05
    assert vs[2]>5
    print(f'queue reverse={reverse}: rear speed={vs[2]:.3f}')
# Independently sample the manually specified 11.5 m cubic used by integration fixtures.
r=11.5
p0=(-10,-1.5);p1=(-10+r*.55228475,-1.5)
p3=(1.5,10);p2=(1.5,10-r*.55228475)
def cubic(t):
    u=1-t
    return tuple(u**3*p0[i]+3*u*u*t*p1[i]+3*u*t*t*p2[i]+t**3*p3[i] for i in range(2))
points=[cubic(i/32) for i in range(33)]
length=sum(math.dist(a,b) for a,b in zip(points,points[1:]))
assert abs(length-math.pi*r/2)<.01
assert all(a[0]<=b[0] and a[1]<=b[1] for a,b in zip(points,points[1:]))
t=.5;u=1-t
first=tuple(3*u*u*(p1[i]-p0[i])+6*u*t*(p2[i]-p1[i])+3*t*t*(p3[i]-p2[i]) for i in range(2))
second=tuple(6*(u*(p2[i]-2*p1[i]+p0[i])+t*(p3[i]-2*p2[i]+p1[i])) for i in range(2))
k=abs(first[0]*second[1]-first[1]*second[0])/math.hypot(*first)**3
cap=math.sqrt(2.5/k)
assert 4<cap<6
print(f'authored quarter-turn: arc={length:.6f} m, midpoint curvature={k:.6f} 1/m, cap={cap:.6f} m/s')
# Scalar stopline budget: front bumper stays upstream and decelerates before stopping.
for dt in [.02,.1]:
    x,v=-80,0
    braked=False
    for _ in range(round(20/dt)):
        remaining=max(0,-16-x-2.25-.1)
        target=min(9,math.sqrt(36+10*remaining)-6,remaining/1.2)
        old=v
        v=max(target,v-5*dt) if target<v else min(target,v+3.5*dt)
        v=min(v,remaining/dt);x+=v*dt
        braked |= 1<v<old
        assert x+2.25 <= -16+1e-6
    assert v<.05 and braked
    print(f'scalar red stop dt={dt}: front={x+2.25:.6f} m, speed={v:.6f} m/s')
print('Independent arithmetic checks only; no Unity/C# execution.')
