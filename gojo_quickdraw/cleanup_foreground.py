"""Local foreground selection from an unbroken wall top and short uprights."""


def wall_in_front(observed, body, tower):
    l,t,r,g=body['bounds'];a,y,b,d=tower['bounds'];scale=max(r-l,g-t)
    if not (l<=a<b<=r and y<t and t-.03*scale<=d<=t+.20*(g-t)):
        return None
    horizontal=[];uprights=[]
    for span in observed.get('spans',[]):
        u,v=span['controls'][0],span['controls'][-1]
        x0,x1=sorted((u[0],v[0]));y0,y1=sorted((u[1],v[1]))
        if (x0<=a and x1>=b and x1-x0>=.65*(r-l) and
                abs(u[1]-t)<.08*scale and abs(v[1]-t)<.08*scale):
            horizontal.append(span['id'])
        if (span['id'] in tower.get('spans',[]) and y1-y0>.45*(t-y) and
                abs(u[0]-v[0])<.20*(b-a) and t-.08*scale<=y1<=t+.20*(g-t)):
            uprights.append(span['id'])
    if horizontal and len(uprights)>=2:
        return {'spans':list(dict.fromkeys(horizontal+uprights)),
                'basis':'Continuous wall top across the upper tower; selected uprights stop near that level.'}
    return None


def ground_support(observed, body, tower):
    """Selected upright intervals that continue through the wall to ground."""
    l,t,r,g=body['bounds'];a,y,b,d=tower['bounds'];support=[]
    for span in observed.get('spans',[]):
        if span['id'] not in tower.get('spans',[]):continue
        u,v=span['controls'][0],span['controls'][-1]
        low,high=sorted((u[1],v[1]));x=(u[0]+v[0])/2
        if (high>=g-.12*(g-t) and low<=t+.10*(g-t) and
                abs(u[0]-v[0])<.35*(high-low) and a-.2*(b-a)<=x<=b+.2*(b-a)):
            support.append(span['id'])
    return support
