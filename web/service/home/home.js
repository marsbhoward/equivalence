(function(){
  var mb=[10,20,30,40], av=[25,50], rift=[10,20,25,30,40];
  function fill(id,from,to){
    var el=document.getElementById(id);
    for(var n=from;n<=to;n++){
      var s=document.createElement('span');
      if(mb.indexOf(n)>=0)s.className='mb';
      if(av.indexOf(n)>=0)s.className='av';
      if(rift.indexOf(n)>=0)s.classList.add('rift');
      if(n===from||n%5===0)s.setAttribute('data-n',n);
      s.title='Floor '+n+(mb.indexOf(n)>=0?' · mini-boss':av.indexOf(n)>=0?' · avatar':'');
      el.appendChild(s);
    }
  }
  fill('f1',1,25); fill('f2',26,50);
})();
