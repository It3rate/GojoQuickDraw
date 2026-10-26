"""Finite rectangle differences; input rectangles and their provenance persist.

Only positive area is removed. Boundary contact is not interior overlap.
The caller constructs separate native rectangles and explicit shared sites.
"""


def intersection(a,b):
    l,t,r,d=max(a[0],b[0]),max(a[1],b[1]),min(a[2],b[2]),min(a[3],b[3])
    return [l,t,r,d] if l<r and t<d else None


def subtract(rectangle, occupied):
    cells=[list(rectangle)]
    for mask in occupied:
        remaining=[]
        for cell in cells:
            overlap=intersection(cell,mask)
            if overlap is None:remaining.append(cell);continue
            l,t,r,b=cell;a,y,c,d=overlap
            for piece in ([l,t,a,b],[c,t,r,b],[a,t,c,y],[a,d,c,b]):
                if piece[0]<piece[2] and piece[1]<piece[3]:remaining.append(piece)
        cells=remaining
    # Join adjacent strips where their union remains a rectangle. This keeps
    # the same area without an unnecessary seam or an arbitrary sliver count.
    changed=True
    while changed:
        changed=False
        for i,a in enumerate(cells):
            for j in range(i+1,len(cells)):
                b=cells[j]
                horizontal=a[1]==b[1] and a[3]==b[3] and (a[2]==b[0] or b[2]==a[0])
                vertical=a[0]==b[0] and a[2]==b[2] and (a[3]==b[1] or b[3]==a[1])
                if horizontal or vertical:
                    cells[i]=[min(a[0],b[0]),min(a[1],b[1]),max(a[2],b[2]),max(a[3],b[3])]
                    cells.pop(j);changed=True;break
            if changed:break
    return sorted(cells)
