const viewer=document.querySelector('#viewer');
const image=viewer.querySelector('img');
document.querySelectorAll('.shot').forEach(button=>button.addEventListener('click',()=>{image.src=button.dataset.src;image.alt=button.dataset.title;viewer.querySelector('p').textContent=button.dataset.title;viewer.showModal();}));
viewer.querySelector('button').addEventListener('click',()=>viewer.close());
viewer.addEventListener('click',event=>{if(event.target===viewer){const r=viewer.getBoundingClientRect();if(event.clientX<r.left||event.clientX>r.right||event.clientY<r.top||event.clientY>r.bottom)viewer.close();}});
