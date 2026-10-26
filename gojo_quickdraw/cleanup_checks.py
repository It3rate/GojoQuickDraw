"""Display reads of the native mirror witnesses; allowances are authored."""


def mirrors(parts, pairs):
    by_id={p['id']:p for p in parts};checks=[]
    for i,pair in enumerate(pairs):
        left,right,axis=(by_id[pair[key]] for key in ('left','right','axisPart'))
        a,b,c=left['bounds'],right['bounds'],axis['bounds']
        residual=[round(a[0]+b[2]-c[0]-c[2],3),round(a[2]+b[0]-c[0]-c[2],3),
                  round(a[1]-b[1],3),round(a[3]-b[3],3)]
        allowance=round(max(.04*(c[2]-c[0]),.08*min(a[3]-a[1],b[3]-b[1])),3)
        failed=max(abs(x) for x in residual)>allowance
        checks.append({'id':'cleanup-mirror-check-'+str(i+1),'kind':'mirror-check',
            'participants':[left['id'],right['id'],axis['id']],
            'status':'needs-review' if failed else 'within-allowance',
            'label':left['role'].capitalize()+' pair symmetry',
            'nativeRead':{'surface':'cleanup.symmetries','index':i},
            'signedResiduals':residual,'allowance':allowance,
            'qualification':'Finite bound differences from the native mirror read; review allowance is authored, not learned.'})
    return checks
