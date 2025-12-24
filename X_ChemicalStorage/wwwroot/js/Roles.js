
//// DataTables initialisation

$('#tableRole').dataTable({

    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,

    layout: {
        topStart: {
            search: {
                placeholder: ''
            }
        },
        topEnd: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: 'بحث', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'إظهار _START_ إلى _END_ من أصل _TOTAL_ سجل', // Customize the info text
        zeroRecords: 'لم يعثر على أي سجل', // No records message
        paginate: {
            first: 'الأول',
            previous: 'السابق',
            next: 'التالي',
            last: 'الأخير'
        }
    }
});

$('#tablePermission').dataTable({

    columnControl: {
        target: 1,
        content: ['search']
    },
    "autoWidth": true,
    "responsive": true,
    "pageLength": 100,
    layout: {
        topStart: {
            search: {
                placeholder: ''
            }
        },
        topEnd: {
            pageLength: {
                menu: [10, 25, 50, 100, 200, 400, 1000]
            }
        },
        bottomEnd: {
            paging: {
                buttons: 6
            }
        }
    },
    language: {
        "autoWidth": true,
        "responsive": true,
        search: 'بحث', // Customize the search input label
        lengthMenu: ' _MENU_ ', // Customize the "Show X entries" label
        info: 'إظهار _START_ إلى _END_ من أصل _TOTAL_ سجل', // Customize the info text
        zeroRecords: 'لم يعثر على أي سجل', // No records message
        paginate: {
            first: 'الأول',
            previous: 'السابق',
            next: 'التالي',
            last: 'الأخير'
        }
    }
});

function Delete(id) {
    Swal.fire({
        title: lbTitleMsgDelete,
        text: lbTextMsgDelete,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: 'secondary',
        confirmButtonText: lbconfirmButtonText,
        cancelButtonText: lbcancelButtonText,
        
    }).then((result) => {
        if (result.isConfirmed) {
            window.location.href = `/Roles/DeleteRole?Id=${id}`;
        }
    })
}

Edit = (id, name, det) => {

    document.getElementById("title").innerHTML = lbTitleEdit;
    document.getElementById("btnSave").value = lbEdit;
    document.getElementById("roleId").value = id;
    document.getElementById("roleName").value = name;
    document.getElementById("roleDetails").value = det;

}

Rest = () => {
    document.getElementById("title").innerHTML = lbAddNewRole;
    document.getElementById("btnSave").value = lbbtnSave;
    document.getElementById("roleId").value = "";
    document.getElementById("roleName").value = "";
    document.getElementById("roleDetails").value = "";

}

