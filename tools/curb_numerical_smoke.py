"""Independent curb geometry/speed calculations, NOT C# or Unity test execution."""
import math

# Exactly the manually specified bay controls in the C# test fixture, in ground metres.
PULL_IN=((20,-1.5),(24,-1.5),(26,-5),(30,-5))
PULL_OUT=((30,-5),(34,-5),(36,-1.5),(40,-1.5))
def at(points,t):
    u=1-t
    return tuple(u**3*points[0][i]+3*u*u*t*points[1][i]+3*u*t*t*points[2][i]+t**3*points[3][i] for i in range(2))
def derivative(points,t):
    u=1-t
    return tuple(3*u*u*(points[1][i]-points[0][i])+6*u*t*(points[2][i]-points[1][i])+3*t*t*(points[3][i]-points[2][i]) for i in range(2))
def cache(points):
    samples=[at(points,i/32) for i in range(33)]
    distances=[0]
    for a,b in zip(samples,samples[1:]):distances.append(distances[-1]+math.dist(a,b))
    return samples,distances

def position(points,distances,s):
    if s>=distances[-1]:return points[-1]
    index=next(i for i in range(32) if distances[i+1]>=s)
    t=(index+(s-distances[index])/(distances[index+1]-distances[index]))/32
    return at(points,t)

lengths=[]
for departure,points in [(False,PULL_IN),(True,PULL_OUT)]:
    samples,distances=cache(points);length=distances[-1];lengths.append(length)
    assert 10<length<12
    assert all(b[0]>a[0] for a,b in zip(samples,samples[1:]))
    assert derivative(points,0)[0]>0 and derivative(points,1)[0]>0
    assert abs(derivative(points,0)[1])<1e-12 and abs(derivative(points,1)[1])<1e-12
    for dt in [.02,.1,.25]:
        s=0.;speed=0.;before=points[0]
        for step in range(20000):
            remaining=length-s
            target=2 if departure else min(2,math.sqrt(10*remaining))
            speed=max(target,speed-5*dt) if target<speed else min(target,speed+3.5*dt)
            travel=min(speed*dt,remaining);s+=travel
            point=position(points,distances,s)
            # Arc is an approximation; allow small cache interpolation error without hiding teleports.
            assert math.dist(point,before)<=speed*dt+.003
            before=point
            if s>=length:break
        else:raise AssertionError('Independent endpoint integration did not complete')
        assert math.dist(before,points[-1])<1e-9
        print(f'curb departure={departure}, dt={dt}: arc={length:.6f}m, steps={step+1}, exact authored endpoint')
# OBB parallel to eastbound lane: its closest edge is 2.5m from lane centre,
# exceeding lane half-width1.5 + body clearance .25. Bay capacity includes clearance.
assert abs(-5-(-1.5))-2/2 >= 3/2+.25
assert 2+2*.25 <= 2.6 and 4.5+2*.25 <= 6
paid=lengths[1]+40+lengths[0]
assert 60<paid<64
print(f'paid bay-to-bay fixture: {paid:.6f} ground m (deadhead/pickup pull-in excluded)')
# Departure lookahead must account for low acceleration, not just pathLength/cruise.
for acceleration in [.1,.5,3.5]:
    duration_accel=min(2/acceleration, math.sqrt(2*lengths[1]/acceleration))
    duration=duration_accel+max(0,lengths[1]-.5*acceleration*duration_accel**2)/2
    assert duration >= lengths[1]/2
    print(f'merge from rest a={acceleration}: conservative duration={duration:.6f}s before headway margin')
print('Independent arithmetic only; no Unity compilation, EditMode or PlayMode tests executed.')
